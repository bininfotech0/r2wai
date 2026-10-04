using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Infrastructure.Security;

namespace R2WAI.Infrastructure.AI.DynamicTools;

/// <summary>One server-advertised tool, as returned by discovery — never executable until an
/// admin commits it to a real, inactive ToolDefinition (implementation plan Phase 3, mirrors
/// IntegrationsController's OpenAPI analyze-then-commit flow).</summary>
public record McpToolCandidate(string Name, string? Description, JsonElement InputSchema);

/// <summary>
/// Wraps the real <c>ModelContextProtocol.Core</c> v2.2.0 client SDK — verified against the actual
/// restored NuGet package via reflection before writing this, not guessed at from documentation.
/// HTTP transport only, matching what was actually verified. Every connection goes through
/// <see cref="EgressGuard"/> first, same SSRF boundary as every other outbound tool call in this
/// codebase (<see cref="DynamicToolExecutor"/>, <c>OpenApiImportService</c>).
/// </summary>
public class McpClientAdapter
{
    private readonly ILogger<McpClientAdapter> _logger;

    public McpClientAdapter(ILogger<McpClientAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<McpToolCandidate>> DiscoverToolsAsync(
        string endpointUrl, string? authHeaderName, string? authHeaderValue, CancellationToken ct = default)
    {
        if (!EgressGuard.IsAllowedUrl(endpointUrl))
            throw new ValidationException("endpointUrl", "This URL is not allowed. Internal network addresses are blocked.");

        try
        {
            await using var client = await ConnectAsync(endpointUrl, authHeaderName, authHeaderValue, ct);
            var tools = await client.ListToolsAsync(cancellationToken: ct);
            return tools.Select(t => new McpToolCandidate(t.Name, t.Description, t.JsonSchema)).ToList();
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP discovery failed for {Endpoint}", endpointUrl);
            throw new ValidationException("endpointUrl", $"Could not connect to the MCP server: {ex.Message}");
        }
    }

    /// <summary>Never throws — matches DynamicToolExecutor.ExecuteAsync's own convention of always
    /// returning a descriptive string, since this is called from a live AI tool invocation where an
    /// unhandled exception would surface as a raw error to the model instead of a clear denial.</summary>
    public async Task<string> CallToolAsync(
        string endpointUrl, string? authHeaderName, string? authHeaderValue,
        string toolName, string? argumentsJson, CancellationToken ct = default)
    {
        if (!EgressGuard.IsAllowedUrl(endpointUrl))
            return "This action targets a network address that is not allowed and cannot be performed.";

        Dictionary<string, object?>? arguments = null;
        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            try
            {
                arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
            }
            catch (JsonException)
            {
                return "The input provided is not valid JSON matching this tool's schema.";
            }
        }

        try
        {
            await using var client = await ConnectAsync(endpointUrl, authHeaderName, authHeaderValue, ct);
            var result = await client.CallToolAsync(toolName, arguments, cancellationToken: ct);

            var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(b => b.Text));
            if (result.IsError == true)
                return $"MCP tool call failed: {(string.IsNullOrWhiteSpace(text) ? "unknown error" : text)}";

            return string.IsNullOrEmpty(text) ? "(no content returned)" : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP tool call to {Endpoint}/{Tool} failed", endpointUrl, toolName);
            return $"MCP tool call failed: {ex.Message}";
        }
    }

    private static async Task<McpClient> ConnectAsync(
        string endpointUrl, string? authHeaderName, string? authHeaderValue, CancellationToken ct)
    {
        var options = new HttpClientTransportOptions { Endpoint = new Uri(endpointUrl) };
        if (!string.IsNullOrEmpty(authHeaderName) && !string.IsNullOrEmpty(authHeaderValue))
            options.AdditionalHeaders = new Dictionary<string, string> { [authHeaderName] = authHeaderValue };

        var transport = new HttpClientTransport(options);
        return await McpClient.CreateAsync(transport, cancellationToken: ct);
    }
}
