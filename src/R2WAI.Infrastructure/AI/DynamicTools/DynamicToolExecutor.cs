using System.Text.Json;
using Microsoft.Extensions.Logging;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Services.ToolFramework;

namespace R2WAI.Infrastructure.AI.DynamicTools;

/// <summary>
/// Executes a governed <see cref="ToolDefinition"/> row as a live API call, dispatching through the
/// existing <see cref="HttpTool"/> executor (retry/circuit-breaker policy included) rather than
/// duplicating HTTP-call logic. This is the missing half of the Tool/Integration Registry bridge:
/// previously a ToolDefinition only ever got *checked* by name (see AiFunctionAuditFilter) — nothing
/// made it actually callable.
///
/// Handles two shapes of ToolDefinition, matching the two ways one gets created today:
/// - ApplicationApi-linked (the governed-capability shape, e.g. from ApplicationWorkspace.razor's
///   Capabilities tab) — auth comes from ApplicationApi.AuthScheme/CredentialRef.
/// - Direct EndpointUrl (what Integrations.razor's "Add Integration" flow actually creates) — auth
///   comes from the AuthType/credential fields already stored in ToolDefinition.Configuration.
/// </summary>
public class DynamicToolExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<DynamicToolExecutor> _logger;

    public DynamicToolExecutor(IToolRegistry toolRegistry, ILogger<DynamicToolExecutor> logger)
    {
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(ToolDefinition toolDef, string? input, CancellationToken ct)
    {
        var target = ResolveTarget(toolDef);
        if (target.ErrorMessage is not null)
        {
            _logger.LogWarning("Dynamic tool {Tool} could not resolve a callable target: {Reason}", toolDef.Name, target.ErrorMessage);
            return target.ErrorMessage;
        }

        var httpTool = _toolRegistry.Get("HttpTool");
        if (httpTool is null)
        {
            _logger.LogError("Dynamic tool {Tool} could not execute — HttpTool is not registered", toolDef.Name);
            return "This action is temporarily unavailable.";
        }

        var context = new ToolContext
        {
            TenantId = toolDef.TenantId,
            CancellationToken = ct,
            Parameters = new Dictionary<string, object?>
            {
                ["baseUrl"] = target.BaseUrl,
                ["method"] = string.IsNullOrWhiteSpace(toolDef.HttpMethod) ? "GET" : toolDef.HttpMethod,
                ["path"] = toolDef.EndpointPath ?? string.Empty,
                ["body"] = input,
                ["authorizationHeader"] = target.AuthorizationHeader
            }
        };

        var result = await httpTool.ExecuteAsync(context);

        return result.Success
            ? result.Data ?? string.Empty
            : $"API call failed: {result.Error ?? "unknown error"}";
    }

    internal sealed record TargetResolution(string? BaseUrl, string? AuthorizationHeader, string? ErrorMessage);

    private static TargetResolution ResolveTarget(ToolDefinition toolDef)
    {
        if (toolDef.ApplicationApi is { } api)
        {
            // CredentialRef resolution (vault lookup, OAuth token exchange, etc.) isn't built yet —
            // a known, tracked gap, not a silent bypass. Failing loudly beats guessing.
            return api.AuthScheme == ApiAuthScheme.None
                ? new TargetResolution(api.BaseUrl, null, null)
                : new TargetResolution(null, null, $"This action requires {api.AuthScheme} authentication, which isn't supported for AI-invoked calls yet.");
        }

        if (!string.IsNullOrWhiteSpace(toolDef.EndpointUrl))
            return ResolveDirectEndpoint(toolDef.EndpointUrl, toolDef.Configuration);

        return new TargetResolution(null, null, $"Tool '{toolDef.Name}' is not linked to a registered API and cannot be called.");
    }

    // Integrations.razor / CreateEditIntegrationDialog.razor already store real credential values
    // (not just a scheme) directly in Configuration — unlike ApplicationApi.CredentialRef, which is
    // an unresolved reference — so these can be applied to the outbound call today, for the auth
    // methods with a well-defined standard HTTP header form. ApiKey is deliberately excluded: the
    // header name/placement it needs is API-specific and unknowable generically, so guessing would
    // produce a misleading "it connected" false negative rather than a real capability.
    internal static TargetResolution ResolveDirectEndpoint(string endpointUrl, string? configurationJson)
    {
        string authType = "None";
        string? credential = null;
        string? password = null;

        if (!string.IsNullOrWhiteSpace(configurationJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(configurationJson);
                var root = doc.RootElement;
                authType = root.TryGetProperty("AuthType", out var at) ? (at.GetString() ?? "None") : "None";
                credential = root.TryGetProperty("Token", out var t) ? t.GetString()
                    : root.TryGetProperty("ApiKey", out var ak) ? ak.GetString()
                    : root.TryGetProperty("Username", out var u) ? u.GetString()
                    : null;
                password = root.TryGetProperty("Password", out var p) ? p.GetString() : null;
            }
            catch (JsonException)
            {
                // Malformed configuration — treat as unauthenticated rather than fail the call outright.
            }
        }

        return authType switch
        {
            "None" or "" => new TargetResolution(endpointUrl, null, null),
            "Bearer" or "OAuth2" when !string.IsNullOrEmpty(credential) =>
                new TargetResolution(endpointUrl, $"Bearer {credential}", null),
            "Basic" when !string.IsNullOrEmpty(credential) =>
                new TargetResolution(endpointUrl, $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{credential}:{password}"))}", null),
            _ => new TargetResolution(null, null, $"This action requires {authType} authentication, which isn't supported for AI-invoked calls yet.")
        };
    }
}
