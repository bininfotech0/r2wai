using System.Text.Json;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Security;
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
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<DynamicToolExecutor> _logger;

    public DynamicToolExecutor(IToolRegistry toolRegistry, IEncryptionService encryptionService, ILogger<DynamicToolExecutor> logger)
    {
        _toolRegistry = toolRegistry;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    // Same eligibility as DynamicToolFunctionFactory.BuildPluginAsync's query — a tool this class can
    // actually dispatch given nothing more than the ToolDefinition itself and a raw input string, no
    // live Kernel/chat session required. Used both to build the agent-callable function (factory) and
    // to decide whether a paused approval can capture a durable, replayable payload (see
    // DeferredToolCallPayload) instead of just denying outright.
    public static bool IsExecutable(ToolDefinition toolDef) =>
        toolDef.ToolType == ToolType.Http && (toolDef.ApplicationApiId != null || toolDef.EndpointUrl != null);

    public async Task<string> ExecuteAsync(ToolDefinition toolDef, string? input, CancellationToken ct)
    {
        var target = ResolveTarget(toolDef, _encryptionService, _logger);
        if (target.ErrorMessage is not null)
        {
            _logger.LogWarning("Dynamic tool {Tool} could not resolve a callable target: {Reason}", toolDef.Name, target.ErrorMessage);
            return target.ErrorMessage;
        }

        // P0-8: every real dispatch, not just the "Test Connection" button, must be blocked from
        // reaching an internal/private address — see EgressGuard's doc comment for why this check
        // used to only exist there.
        if (!Security.EgressGuard.IsAllowedUrl(target.BaseUrl))
        {
            _logger.LogWarning("Dynamic tool {Tool} blocked — target address is not allowed: {BaseUrl}", toolDef.Name, target.BaseUrl);
            return "This action targets a network address that is not allowed and cannot be performed.";
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
                ["authorizationHeader"] = target.AuthorizationHeader,
                ["extraHeaderName"] = target.ExtraHeaderName,
                ["extraHeaderValue"] = target.ExtraHeaderValue
            }
        };

        var result = await httpTool.ExecuteAsync(context);

        return result.Success
            ? result.Data ?? string.Empty
            : $"API call failed: {result.Error ?? "unknown error"}";
    }

    internal sealed record TargetResolution(
        string? BaseUrl, string? AuthorizationHeader, string? ErrorMessage,
        string? ExtraHeaderName = null, string? ExtraHeaderValue = null);

    private static TargetResolution ResolveTarget(ToolDefinition toolDef, IEncryptionService encryptionService, ILogger logger)
    {
        if (toolDef.ApplicationApi is { } api)
            return ResolveApplicationApiTarget(api, encryptionService, logger);

        if (!string.IsNullOrWhiteSpace(toolDef.EndpointUrl))
            return ResolveDirectEndpoint(toolDef.EndpointUrl, toolDef.Configuration, encryptionService, logger);

        return new TargetResolution(null, null, $"Tool '{toolDef.Name}' is not linked to a registered API and cannot be called.");
    }

    // Static/long-lived credentials only — OAuth2/EntraId here means "a token was pasted in at
    // registration time", not a live authorization-code/client-credentials exchange. That's a
    // deliberate scope cut (see Track B Phase 3b decisions), not an oversight.
    private static TargetResolution ResolveApplicationApiTarget(ApplicationApi api, IEncryptionService encryptionService, ILogger logger)
    {
        if (api.AuthScheme == ApiAuthScheme.None)
            return new TargetResolution(api.BaseUrl, null, null);

        if (string.IsNullOrEmpty(api.CredentialSecretEncrypted))
            return new TargetResolution(null, null, $"This action requires {api.AuthScheme} authentication, but no credential is configured for this API.");

        string secret;
        try
        {
            secret = encryptionService.Decrypt(api.CredentialSecretEncrypted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to decrypt stored credential for ApplicationApi {ApiId}", api.Id);
            return new TargetResolution(null, null, "The stored credential for this API could not be decrypted.");
        }

        if (api.AuthScheme == ApiAuthScheme.ApiKey)
        {
            return string.IsNullOrEmpty(api.CredentialHeaderName)
                ? new TargetResolution(null, null, "ApiKey authentication is configured but no header name was set.")
                : new TargetResolution(api.BaseUrl, null, null, api.CredentialHeaderName, secret);
        }

        // OAuth2 / Jwt / EntraId: treated as a static bearer token.
        return new TargetResolution(api.BaseUrl, $"Bearer {secret}", null);
    }

    // CreateEditIntegrationDialog.tsx stores real credential values (not just a scheme) directly in
    // Configuration — unlike ApplicationApi.CredentialRef, which is an unresolved reference — so these
    // can be applied to the outbound call today, for the auth methods with a well-defined standard HTTP
    // header form. ApiKey requires a header name, which the admin supplies at registration
    // (ApiKeyHeaderName) — same pattern as Track B Phase 3b's ApplicationApi.CredentialHeaderName.
    //
    // Token/ApiKey/Password are encrypted at rest (IntegrationCredentialCodec) — decrypted here, right
    // before use, never persisted or logged in plaintext.
    internal static TargetResolution ResolveDirectEndpoint(
        string endpointUrl, string? configurationJson, IEncryptionService encryptionService, ILogger? logger = null)
    {
        configurationJson = IntegrationCredentialCodec.DecryptSecrets(configurationJson, encryptionService, logger);

        string authType = "None";
        string? credential = null;
        string? password = null;
        string? apiKeyHeaderName = null;

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
                apiKeyHeaderName = root.TryGetProperty("ApiKeyHeaderName", out var hn) ? hn.GetString() : null;
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
            "ApiKey" when !string.IsNullOrEmpty(credential) && !string.IsNullOrEmpty(apiKeyHeaderName) =>
                new TargetResolution(endpointUrl, null, null, apiKeyHeaderName, credential),
            "ApiKey" =>
                new TargetResolution(null, null, "ApiKey authentication requires both an API key and a header name."),
            _ => new TargetResolution(null, null, $"This action requires {authType} authentication, which isn't supported for AI-invoked calls yet.")
        };
    }
}
