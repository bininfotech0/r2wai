using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers McpDynamicToolExecutor — the MCP counterpart to DynamicToolExecutorTests. No real MCP
/// server is involved: EgressGuard blocks every private/internal target before McpClientAdapter ever
/// opens a connection, so the denial paths below never touch the network, and the "not linked"/
/// "credential could not be decrypted" paths never reach McpClientAdapter at all.
/// </summary>
public class McpDynamicToolExecutorTests
{
    private sealed class FakeEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText) => "enc:" + plainText;
        public string Decrypt(string cipherText) => cipherText.StartsWith("enc:", StringComparison.Ordinal)
            ? cipherText["enc:".Length..]
            : throw new FormatException("Not a value this fake encrypted.");
    }

    private static McpDynamicToolExecutor CreateExecutor(IEncryptionService? encryptionService = null) =>
        new(new McpClientAdapter(NullLogger<McpClientAdapter>.Instance),
            encryptionService ?? new FakeEncryptionService(),
            NullLogger<McpDynamicToolExecutor>.Instance);

    // McpServerConnection is an EF navigation property with a private setter — populated by EF when
    // the owning query includes it, not settable through ToolDefinition's public API. Mirrors
    // DynamicToolExecutorTests.AttachApplicationApi's reflection stand-in for what EF does at runtime.
    private static void AttachConnection(ToolDefinition toolDef, McpServerConnection connection)
    {
        typeof(ToolDefinition).GetProperty(nameof(ToolDefinition.McpServerConnection), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(toolDef, connection);
    }

    private static ToolDefinition CreateMcpToolDef(McpServerConnection? connection, string mcpToolName = "get_status")
    {
        var toolDef = new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "Get Status", ToolType.Mcp, "desc");
        if (connection is not null)
        {
            toolDef.LinkMcpServer(connection.Id, mcpToolName);
            AttachConnection(toolDef, connection);
        }
        return toolDef;
    }

    private static McpServerConnection CreateConnection(string endpointUrl, string? authHeaderName = null, string? credentialEncrypted = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Test MCP Server", endpointUrl, authHeaderName, credentialEncrypted);

    [Theory]
    [InlineData(ToolType.Mcp, true, "get_status", true)]
    [InlineData(ToolType.Http, true, "get_status", false)]
    [InlineData(ToolType.Mcp, false, "get_status", false)]
    [InlineData(ToolType.Mcp, true, null, false)]
    public void IsExecutable_MatchesOnlyFullyLinkedMcpTools(ToolType toolType, bool hasConnectionId, string? mcpToolName, bool expected)
    {
        var toolDef = new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "Tool", toolType, "desc");
        if (hasConnectionId && mcpToolName is not null)
            toolDef.LinkMcpServer(Guid.NewGuid(), mcpToolName);

        Assert.Equal(expected, McpDynamicToolExecutor.IsExecutable(toolDef));
    }

    [Fact]
    public async Task ExecuteAsync_NoLinkedConnection_ReturnsGuardMessage_WithoutCallingMcpClient()
    {
        var executor = CreateExecutor();
        var toolDef = CreateMcpToolDef(connection: null);

        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("not linked to a registered MCP server", result);
    }

    [Theory]
    [InlineData("http://169.254.169.254")] // cloud metadata endpoint
    [InlineData("http://10.0.0.5")]
    [InlineData("http://localhost")]
    public async Task ExecuteAsync_ConnectionTargetsAPrivateOrInternalAddress_ReturnsGuardMessage(string blockedUrl)
    {
        var executor = CreateExecutor();
        var connection = CreateConnection(blockedUrl);
        var toolDef = CreateMcpToolDef(connection);

        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("not allowed", result);
    }

    [Fact]
    public async Task ExecuteAsync_StoredCredentialFailsToDecrypt_ReturnsGuardMessage_WithoutCallingMcpClient()
    {
        var executor = CreateExecutor();
        // "not-a-real-ciphertext" doesn't start with "enc:" — FakeEncryptionService.Decrypt throws,
        // same failure mode DynamicToolExecutorTests relies on for the real EncryptionService's own
        // bad-base64 failure.
        var connection = CreateConnection("https://mcp.example.com", credentialEncrypted: "not-a-real-ciphertext");
        var toolDef = CreateMcpToolDef(connection);

        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("could not be decrypted", result);
    }
}
