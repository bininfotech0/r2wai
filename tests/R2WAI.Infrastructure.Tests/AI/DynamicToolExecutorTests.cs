using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.AI.DynamicTools;
using R2WAI.Infrastructure.Services.ToolFramework;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers DynamicToolExecutor — the piece that dispatches a governed ToolDefinition to the shared
/// HttpTool executor. Uses a fake ITool/IToolRegistry so no real HTTP call happens; the point is to
/// verify the routing/guard logic (missing API link, unsupported auth scheme, HttpTool absent,
/// success/failure mapping), not HttpTool itself (covered separately).
/// </summary>
public class DynamicToolExecutorTests
{
    private sealed class FakeTool : ITool
    {
        public string Name => "HttpTool";
        public string Description => "fake";
        public ToolContext? LastContext { get; private set; }
        public ToolResult Result { get; set; } = new() { Success = true, Data = "ok" };

        public Task<ToolResult> ExecuteAsync(ToolContext context)
        {
            LastContext = context;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
        public void Register(ITool tool) => _tools[tool.Name] = tool;
        public ITool? Get(string name) => _tools.GetValueOrDefault(name);
        public IEnumerable<ITool> GetAll() => _tools.Values;
    }

    // ApplicationApi is an EF navigation property with a private setter — populated by EF when the
    // owning query includes it, not settable through ToolDefinition's public API. Reflection here
    // stands in for what EF does at runtime.
    private static void AttachApplicationApi(ToolDefinition toolDef, ApplicationApi api)
    {
        typeof(ToolDefinition).GetProperty(nameof(ToolDefinition.ApplicationApi), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(toolDef, api);
    }

    private static ToolDefinition CreateToolDef(ApplicationApi? api, string httpMethod = "GET", string path = "status")
    {
        var toolDef = new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "Get Status", ToolType.Http, "desc");
        if (api is not null)
        {
            toolDef.LinkApi(api.Id, httpMethod, path);
            AttachApplicationApi(toolDef, api);
        }
        return toolDef;
    }

    private static ApplicationApi CreateApi(ApiAuthScheme scheme = ApiAuthScheme.None) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test API", "https://api.example.com", scheme);

    [Fact]
    public async Task ExecuteAsync_NoLinkedApi_ReturnsGuardMessage_WithoutCallingHttpTool()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateToolDef(api: null);
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("not linked to a registered API", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Theory]
    [InlineData(ApiAuthScheme.ApiKey)]
    [InlineData(ApiAuthScheme.OAuth2)]
    [InlineData(ApiAuthScheme.Jwt)]
    [InlineData(ApiAuthScheme.EntraId)]
    public async Task ExecuteAsync_UnsupportedAuthScheme_ReturnsGuardMessage_WithoutCallingHttpTool(ApiAuthScheme scheme)
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateToolDef(CreateApi(scheme));
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("authentication", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task ExecuteAsync_HttpToolNotRegistered_ReturnsUnavailableMessage()
    {
        var registry = new FakeRegistry(); // no "HttpTool" registered
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateToolDef(CreateApi());
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("This action is temporarily unavailable.", result);
    }

    [Fact]
    public async Task ExecuteAsync_NoneAuthScheme_DispatchesToHttpTool_WithExpectedParameters()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool { Result = new ToolResult { Success = true, Data = "{\"status\":\"ok\"}" } };
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var api = CreateApi();
        var toolDef = CreateToolDef(api, httpMethod: "POST", path: "orders");
        var result = await executor.ExecuteAsync(toolDef, input: "{\"id\":1}", CancellationToken.None);

        Assert.Equal("{\"status\":\"ok\"}", result);
        Assert.NotNull(fakeTool.LastContext);
        Assert.Equal(api.BaseUrl, fakeTool.LastContext!.Parameters["baseUrl"]);
        Assert.Equal("POST", fakeTool.LastContext.Parameters["method"]);
        Assert.Equal("orders", fakeTool.LastContext.Parameters["path"]);
        Assert.Equal("{\"id\":1}", fakeTool.LastContext.Parameters["body"]);
    }

    [Fact]
    public async Task ExecuteAsync_HttpToolReportsFailure_ReturnsFailureMessage()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool { Result = new ToolResult { Success = false, Error = "timeout" } };
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateToolDef(CreateApi());
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("API call failed: timeout", result);
    }

    // ── Direct-EndpointUrl shape (what Integrations.razor's "Add Integration" actually creates —
    //    EndpointUrl set straight on ToolDefinition, no ApplicationApi link) ──

    private static ToolDefinition CreateDirectToolDef(string endpointUrl, string? configuration = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Slack Notify", ToolType.Http, "desc", endpointUrl, configuration);

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_NoAuth_DispatchesToHttpTool()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool { Result = new ToolResult { Success = true, Data = "ok" } };
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"None"}""");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal("https://api.example.com", fakeTool.LastContext!.Parameters["baseUrl"]);
        Assert.Null(fakeTool.LastContext.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_BearerAuth_AppliesAuthorizationHeader()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"Bearer","Token":"secret-token"}""");
        await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("Bearer secret-token", fakeTool.LastContext!.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_BasicAuth_AppliesBase64Header()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"Basic","Username":"alice","Password":"hunter2"}""");
        await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        var expected = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("alice:hunter2"));
        Assert.Equal(expected, fakeTool.LastContext!.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_ApiKeyAuth_ReturnsGuardMessage_WithoutCallingHttpTool()
    {
        // ApiKey's header name/placement is API-specific and unknowable generically — guessing wrong
        // would produce a misleading "it connected" false positive, so this must stay unsupported.
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"ApiKey","ApiKey":"key123"}""");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("ApiKey authentication", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_MalformedConfiguration_TreatedAsUnauthenticated()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = CreateDirectToolDef("https://api.example.com", "{not valid json");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.NotNull(fakeTool.LastContext); // dispatched, not blocked
        Assert.Null(fakeTool.LastContext!.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_NeitherApplicationApiNorEndpointUrl_ReturnsGuardMessage()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = new DynamicToolExecutor(registry, NullLogger<DynamicToolExecutor>.Instance);

        var toolDef = new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "Orphan Tool", ToolType.Http, "desc");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("not linked to a registered API", result);
        Assert.Null(fakeTool.LastContext);
    }
}
