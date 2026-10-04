using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using R2WAI.Application.Common.Interfaces;
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

    // Reversible stand-in for the real AES-256-GCM EncryptionService — these are unit tests for
    // DynamicToolExecutor's routing logic, not for encryption itself. Throws FormatException (not some
    // arbitrary exception type) for a value this fake didn't encrypt, matching the real
    // EncryptionService.Decrypt's actual failure mode (bad base64) — IntegrationCredentialCodec's
    // legacy-plaintext fallback specifically catches that, so this fake needs to fail the same way to
    // be a faithful stand-in.
    private sealed class FakeEncryptionService : IEncryptionService
    {
        public string Encrypt(string plainText) => "enc:" + plainText;
        public string Decrypt(string cipherText) => cipherText.StartsWith("enc:", StringComparison.Ordinal)
            ? cipherText["enc:".Length..]
            : throw new FormatException("Not a value this fake encrypted.");
    }

    private static DynamicToolExecutor CreateExecutor(IToolRegistry registry) =>
        new(registry, new FakeEncryptionService(), NullLogger<DynamicToolExecutor>.Instance);

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

    private static ApplicationApi CreateApiWithCredential(ApiAuthScheme scheme, string secret, string? headerName = null)
    {
        var api = CreateApi(scheme);
        api.SetCredential("enc:" + secret, headerName);
        return api;
    }

    [Fact]
    public async Task ExecuteAsync_NoLinkedApi_ReturnsGuardMessage_WithoutCallingHttpTool()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

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
    public async Task ExecuteAsync_NonNoneAuthSchemeWithoutCredential_ReturnsGuardMessage_WithoutCallingHttpTool(ApiAuthScheme scheme)
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateToolDef(CreateApi(scheme)); // no credential configured
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("authentication", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Theory]
    [InlineData(ApiAuthScheme.OAuth2)]
    [InlineData(ApiAuthScheme.Jwt)]
    [InlineData(ApiAuthScheme.EntraId)]
    public async Task ExecuteAsync_StaticTokenAuthScheme_WithCredential_AppliesBearerHeader(ApiAuthScheme scheme)
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateToolDef(CreateApiWithCredential(scheme, "secret-token"));
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.NotNull(fakeTool.LastContext);
        Assert.Equal("Bearer secret-token", fakeTool.LastContext!.Parameters["authorizationHeader"]);
        Assert.DoesNotContain("secret-token", result); // never echoed back to the caller
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationApi_ApiKeyWithCredentialAndHeaderName_AppliesExtraHeader()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateToolDef(CreateApiWithCredential(ApiAuthScheme.ApiKey, "key-value", "X-Api-Key"));
        await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.NotNull(fakeTool.LastContext);
        Assert.Equal("X-Api-Key", fakeTool.LastContext!.Parameters["extraHeaderName"]);
        Assert.Equal("key-value", fakeTool.LastContext.Parameters["extraHeaderValue"]);
        Assert.Null(fakeTool.LastContext.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationApi_ApiKeyCredentialWithoutHeaderName_ReturnsGuardMessage()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateToolDef(CreateApiWithCredential(ApiAuthScheme.ApiKey, "key-value", headerName: null));
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("header name", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task ExecuteAsync_ApplicationApi_UndecryptableCredential_ReturnsGuardMessage_WithoutCallingHttpTool()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var api = CreateApi(ApiAuthScheme.OAuth2);
        api.SetCredential("not-a-value-this-fake-encrypted", null);
        var toolDef = CreateToolDef(api);
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("could not be decrypted", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task ExecuteAsync_HttpToolNotRegistered_ReturnsUnavailableMessage()
    {
        var registry = new FakeRegistry(); // no "HttpTool" registered
        var executor = CreateExecutor(registry);

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
        var executor = CreateExecutor(registry);

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
        var executor = CreateExecutor(registry);

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
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"None"}""");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal("https://api.example.com", fakeTool.LastContext!.Parameters["baseUrl"]);
        Assert.Null(fakeTool.LastContext.Parameters["authorizationHeader"]);
    }

    // Plaintext here, not "enc:"-prefixed — a row saved before Configuration encryption shipped.
    // IntegrationCredentialCodec.DecryptSecrets falls back to the original value on a decrypt failure
    // rather than throwing (see its own doc comment), so an old integration keeps working unchanged.
    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_BearerAuth_AppliesAuthorizationHeader()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"Bearer","Token":"secret-token"}""");
        await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("Bearer secret-token", fakeTool.LastContext!.Parameters["authorizationHeader"]);
    }

    // The real, post-encryption shape: Configuration.Token is ciphertext (as IntegrationCredentialCodec
    // .EncryptSecrets would have stored it), and the outbound call must carry the DECRYPTED value, not
    // the ciphertext itself.
    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_EncryptedBearerToken_DecryptsBeforeApplyingTheHeader()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry); // FakeEncryptionService: Decrypt("enc:x") -> "x"

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"Bearer","Token":"enc:secret-token"}""");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Equal("Bearer secret-token", fakeTool.LastContext!.Parameters["authorizationHeader"]);
        Assert.DoesNotContain("enc:", result); // ciphertext form never leaks into the result either
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_BasicAuth_AppliesBase64Header()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"Basic","Username":"alice","Password":"hunter2"}""");
        await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        var expected = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("alice:hunter2"));
        Assert.Equal(expected, fakeTool.LastContext!.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_ApiKeyAuthWithoutHeaderName_ReturnsGuardMessage_WithoutCallingHttpTool()
    {
        // ApiKey's header name/placement is API-specific and unknowable generically — without an
        // explicit ApiKeyHeaderName, guessing wrong would produce a misleading "it connected" false
        // positive, so this stays guarded (see the header-name-provided case below for the supported path).
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef("https://api.example.com", """{"AuthType":"ApiKey","ApiKey":"key123"}""");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("ApiKey authentication", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_ApiKeyAuthWithHeaderName_AppliesExtraHeader()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef(
            "https://api.example.com",
            """{"AuthType":"ApiKey","ApiKey":"key123","ApiKeyHeaderName":"X-Api-Key"}""");
        await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.NotNull(fakeTool.LastContext);
        Assert.Equal("X-Api-Key", fakeTool.LastContext!.Parameters["extraHeaderName"]);
        Assert.Equal("key123", fakeTool.LastContext.Parameters["extraHeaderValue"]);
        Assert.Null(fakeTool.LastContext.Parameters["authorizationHeader"]);
    }

    [Fact]
    public async Task ExecuteAsync_DirectEndpointUrl_MalformedConfiguration_TreatedAsUnauthenticated()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef("https://api.example.com", "{not valid json");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.NotNull(fakeTool.LastContext); // dispatched, not blocked
        Assert.Null(fakeTool.LastContext!.Parameters["authorizationHeader"]);
    }

    // P0-8: the same EgressGuard rule that already gated IntegrationsController's "Test Connection"
    // button now also gates the real dispatch path — see DynamicToolExecutor.ExecuteAsync.
    [Theory]
    [InlineData("http://169.254.169.254")] // cloud metadata endpoint
    [InlineData("http://10.0.0.5")]
    [InlineData("http://localhost")]
    public async Task ExecuteAsync_TargetIsAPrivateOrInternalAddress_ReturnsGuardMessage_WithoutCallingHttpTool(string blockedUrl)
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = CreateDirectToolDef(blockedUrl, """{"AuthType":"None"}""");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("not allowed", result);
        Assert.Null(fakeTool.LastContext);
    }

    [Fact]
    public async Task ExecuteAsync_NeitherApplicationApiNorEndpointUrl_ReturnsGuardMessage()
    {
        var registry = new FakeRegistry();
        var fakeTool = new FakeTool();
        registry.Register(fakeTool);
        var executor = CreateExecutor(registry);

        var toolDef = new ToolDefinition(Guid.NewGuid(), Guid.NewGuid(), "Orphan Tool", ToolType.Http, "desc");
        var result = await executor.ExecuteAsync(toolDef, input: null, CancellationToken.None);

        Assert.Contains("not linked to a registered API", result);
        Assert.Null(fakeTool.LastContext);
    }
}
