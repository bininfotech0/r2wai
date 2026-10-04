using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;

namespace R2WAI.Infrastructure.AI.DynamicTools;

/// <summary>
/// The MCP counterpart to <see cref="DynamicToolExecutor"/> — same role (turn a governed
/// <see cref="ToolDefinition"/> row into a live call given nothing but the row and a raw input
/// string), same "never throws, always returns a descriptive string" contract, dispatching through
/// <see cref="McpClientAdapter"/> instead of <c>HttpTool</c>. Kept as its own class rather than
/// folded into <see cref="DynamicToolExecutor"/>: the two have no shared dispatch logic (MCP has no
/// HTTP method/path/body shape), only a shared *role*.
/// </summary>
public class McpDynamicToolExecutor
{
    private readonly McpClientAdapter _mcpClient;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<McpDynamicToolExecutor> _logger;

    public McpDynamicToolExecutor(McpClientAdapter mcpClient, IEncryptionService encryptionService, ILogger<McpDynamicToolExecutor> logger)
    {
        _mcpClient = mcpClient;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    // Same role as DynamicToolExecutor.IsExecutable — decides both agent-callable-function
    // eligibility (DynamicToolFunctionFactory) and deferred-approval-replay eligibility (the
    // Tool Gateway's DenyApprovalRequired branch).
    public static bool IsExecutable(ToolDefinition toolDef) =>
        toolDef.ToolType == ToolType.Mcp && toolDef.McpServerConnectionId is not null && !string.IsNullOrEmpty(toolDef.McpToolName);

    public async Task<string> ExecuteAsync(ToolDefinition toolDef, string? input, CancellationToken ct)
    {
        var connection = toolDef.McpServerConnection;
        if (connection is null || toolDef.McpToolName is null)
            return $"Tool '{toolDef.Name}' is not linked to a registered MCP server and cannot be called.";

        string? credential = null;
        if (!string.IsNullOrEmpty(connection.CredentialEncrypted))
        {
            try
            {
                credential = _encryptionService.Decrypt(connection.CredentialEncrypted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt stored credential for McpServerConnection {ConnectionId}", connection.Id);
                return "The stored credential for this MCP server could not be decrypted.";
            }
        }

        return await _mcpClient.CallToolAsync(
            connection.EndpointUrl, connection.AuthHeaderName, credential, toolDef.McpToolName, input, ct);
    }
}
