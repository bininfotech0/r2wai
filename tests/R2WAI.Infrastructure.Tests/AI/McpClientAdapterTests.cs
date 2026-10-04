using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers McpClientAdapter's EgressGuard gate — mirrors OpenApiImportServiceTests' SSRF-denial
/// coverage for the analogous discover/call surface. Every case here is rejected before any transport
/// is constructed, so no real MCP server is needed and no network call happens.
/// </summary>
public class McpClientAdapterTests
{
    private static McpClientAdapter CreateAdapter() => new(NullLogger<McpClientAdapter>.Instance);

    [Theory]
    [InlineData("http://[::1]/mcp")]
    [InlineData("http://169.254.169.254/mcp")]
    [InlineData("http://10.0.0.5/mcp")]
    [InlineData("http://localhost/mcp")]
    public async Task DiscoverToolsAsync_PrivateOrInternalAddress_ThrowsValidation_NeverConnects(string blockedUrl)
    {
        var adapter = CreateAdapter();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => adapter.DiscoverToolsAsync(blockedUrl, authHeaderName: null, authHeaderValue: null));

        Assert.Contains("endpointUrl", ex.Errors.Keys);
        Assert.Contains("not allowed", ex.Errors["endpointUrl"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://[::1]/mcp")]
    [InlineData("http://169.254.169.254/mcp")]
    [InlineData("http://10.0.0.5/mcp")]
    public async Task CallToolAsync_PrivateOrInternalAddress_ReturnsGuardMessage_NeverConnects(string blockedUrl)
    {
        var adapter = CreateAdapter();

        var result = await adapter.CallToolAsync(blockedUrl, authHeaderName: null, authHeaderValue: null, toolName: "get_status", argumentsJson: null);

        Assert.Contains("not allowed", result);
    }

    [Fact]
    public async Task CallToolAsync_MalformedArgumentsJson_ReturnsGuardMessage_NeverConnects()
    {
        var adapter = CreateAdapter();

        // A publicly-routable-looking host so the malformed-JSON check is what's actually exercised —
        // if this ever regressed to check arguments after connecting, this test would hang/fail on a
        // real connection attempt instead of returning immediately.
        var result = await adapter.CallToolAsync(
            "https://mcp.example.com", authHeaderName: null, authHeaderValue: null,
            toolName: "get_status", argumentsJson: "{not valid json");

        Assert.Contains("not valid JSON", result);
    }
}
