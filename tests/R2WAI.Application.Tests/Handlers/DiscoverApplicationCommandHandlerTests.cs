using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

public class DiscoverApplicationCommandHandlerTests
{
    private readonly Mock<IRepository<ConnectedApplication>> _applicationRepoMock = new();
    private readonly Mock<IRepository<ApplicationApi>> _apiRepoMock = new();
    private readonly Mock<IRepository<ToolDefinition>> _capabilityRepoMock = new();
    private readonly Mock<IOpenApiImportService> _openApiImportServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ConnectedApplication _application;

    public DiscoverApplicationCommandHandlerTests()
    {
        _application = new ConnectedApplication(Guid.NewGuid(), _tenantId, Guid.NewGuid(), "Property Tax", "PROP");

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _applicationRepoMock.Setup(r => r.GetByIdAsync(_application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_application);
        _apiRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationApi, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationApi>)[]);
        _capabilityRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ToolDefinition, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ToolDefinition>)[]);
    }

    private DiscoverApplicationCommandHandler CreateHandler() =>
        new(_applicationRepoMock.Object, _apiRepoMock.Object, _capabilityRepoMock.Object,
            _openApiImportServiceMock.Object, _currentUserMock.Object, _uowMock.Object,
            NullLogger<DiscoverApplicationCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ValidSpec_CreatesApiAndCapabilities()
    {
        _openApiImportServiceMock.Setup(s => s.AnalyzeAsync("https://api.example.com/openapi.json", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenApiAnalyzeResult("https://api.example.com", [
                new OpenApiOperationCandidate("GET", "/status", "getStatus", "Get status"),
                new OpenApiOperationCandidate("POST", "/records", "createRecord", "Create a record"),
            ]));

        var handler = CreateHandler();
        var result = await handler.Handle(new DiscoverApplicationCommand
        {
            ApplicationId = _application.Id,
            OpenApiUrl = "https://api.example.com/openapi.json"
        }, CancellationToken.None);

        Assert.Equal("https://api.example.com", result.BaseUrl);
        Assert.Equal(2, result.OperationsDiscovered);
        Assert.Equal(2, result.CapabilitiesCreated);
        Assert.Equal(0, result.CapabilitiesSkippedAsExisting);
        Assert.Equal(ApplicationStatus.Configuring, _application.Status);

        _apiRepoMock.Verify(r => r.AddAsync(It.Is<ApplicationApi>(a => a.BaseUrl == "https://api.example.com"), It.IsAny<CancellationToken>()), Times.Once);
        _capabilityRepoMock.Verify(r => r.AddAsync(It.IsAny<ToolDefinition>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OperationAlreadyExistsAsCapability_SkipsDuplicate()
    {
        var applicationApi = new ApplicationApi(Guid.NewGuid(), _tenantId, _application.Id, "Existing API", "https://api.example.com");
        _apiRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationApi, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationApi>)[applicationApi]);

        var existingCapability = new ToolDefinition(Guid.NewGuid(), _tenantId, "getStatus", ToolType.Http, "already here");
        existingCapability.LinkApi(applicationApi.Id, "GET", "/status");
        _capabilityRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ToolDefinition, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ToolDefinition>)[existingCapability]);

        _openApiImportServiceMock.Setup(s => s.AnalyzeAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenApiAnalyzeResult("https://api.example.com", [
                new OpenApiOperationCandidate("GET", "/status", "getStatus", "Get status"),
                new OpenApiOperationCandidate("POST", "/records", "createRecord", "Create a record"),
            ]));

        var handler = CreateHandler();
        var result = await handler.Handle(new DiscoverApplicationCommand
        {
            ApplicationId = _application.Id,
            OpenApiUrl = "https://api.example.com/openapi.json"
        }, CancellationToken.None);

        Assert.Equal(1, result.CapabilitiesCreated);
        Assert.Equal(1, result.CapabilitiesSkippedAsExisting);
        _apiRepoMock.Verify(r => r.AddAsync(It.IsAny<ApplicationApi>(), It.IsAny<CancellationToken>()), Times.Never);
        _capabilityRepoMock.Verify(r => r.AddAsync(It.IsAny<ToolDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownApplication_ThrowsNotFoundException()
    {
        _applicationRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConnectedApplication?)null);

        var handler = CreateHandler();
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DiscoverApplicationCommand
        {
            ApplicationId = Guid.NewGuid(),
            OpenApiUrl = "https://api.example.com/openapi.json"
        }, CancellationToken.None));
    }

    private ApplicationApi SetupExistingApi(string baseUrl = "https://api.example.com")
    {
        var api = new ApplicationApi(Guid.NewGuid(), _tenantId, _application.Id, "Existing API", baseUrl);
        _apiRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationApi, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationApi>)[api]);
        return api;
    }

    private void SetupCapabilities(params ToolDefinition[] capabilities) =>
        _capabilityRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ToolDefinition, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ToolDefinition>)capabilities);

    private void SetupSpec(string baseUrl, params OpenApiOperationCandidate[] operations) =>
        _openApiImportServiceMock.Setup(s => s.AnalyzeAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenApiAnalyzeResult(baseUrl, operations));

    private Task<ApplicationDiscoveryResultDto> Discover() =>
        CreateHandler().Handle(new DiscoverApplicationCommand
        {
            ApplicationId = _application.Id,
            OpenApiUrl = "https://api.example.com/openapi.json"
        }, CancellationToken.None);

    [Fact]
    public async Task Handle_NameTakenByAnotherSystemsCapability_PrefixesWithApplicationCode()
    {
        // Another system's "getStatus" would make this one's silently uncallable at runtime.
        SetupCapabilities(new ToolDefinition(Guid.NewGuid(), _tenantId, "getStatus", ToolType.Http, "other system"));
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/status", "getStatus", "Get status"));

        var result = await Discover();

        Assert.Equal(1, result.CapabilitiesCreated);
        Assert.Equal(1, result.CapabilitiesRenamedForUniqueness);
        _capabilityRepoMock.Verify(r => r.AddAsync(It.Is<ToolDefinition>(t => t.Name == "PROP_getStatus"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NamesCollidingOnlyAfterSanitizing_AreTreatedAsTaken()
    {
        SetupCapabilities(new ToolDefinition(Guid.NewGuid(), _tenantId, "get-status", ToolType.Http, "other system"));
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/status", "get_status", "Get status"));

        await Discover();

        _capabilityRepoMock.Verify(r => r.AddAsync(It.Is<ToolDefinition>(t => t.Name == "PROP_get_status"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateOperationIdsInOneSpec_EachGetsADistinctName()
    {
        SetupSpec("https://api.example.com",
            new OpenApiOperationCandidate("GET", "/a", "list", null),
            new OpenApiOperationCandidate("GET", "/b", "list", null),
            new OpenApiOperationCandidate("GET", "/c", "list", null));

        var added = new List<string>();
        _capabilityRepoMock.Setup(r => r.AddAsync(It.IsAny<ToolDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<ToolDefinition, CancellationToken>((t, _) => added.Add(t.Name));

        var result = await Discover();

        Assert.Equal(3, result.CapabilitiesCreated);
        Assert.Equal(["list", "PROP_list", "PROP_list_2"], added);
    }

    [Fact]
    public async Task Handle_ResyncSameSpec_IsIdempotent()
    {
        var api = SetupExistingApi();
        var renamed = new ToolDefinition(Guid.NewGuid(), _tenantId, "PROP_getStatus", ToolType.Http, null);
        renamed.LinkApi(api.Id, "GET", "/status");
        SetupCapabilities(renamed);
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/status", "getStatus", null));

        var result = await Discover();

        Assert.Equal(0, result.CapabilitiesCreated);
        Assert.Equal(1, result.CapabilitiesSkippedAsExisting);
        _capabilityRepoMock.Verify(r => r.AddAsync(It.IsAny<ToolDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InactiveDraftEndpointMoved_IsRelinked()
    {
        var api = SetupExistingApi();
        var draft = new ToolDefinition(Guid.NewGuid(), _tenantId, "getStatus", ToolType.Http, null);
        draft.LinkApi(api.Id, "GET", "/status");
        draft.Deactivate();
        SetupCapabilities(draft);
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/v2/status", "getStatus", null));

        var result = await Discover();

        Assert.Equal(1, result.CapabilitiesRelinked);
        Assert.Equal(0, result.StaleCapabilityCount);
        Assert.Equal("/v2/status", draft.EndpointPath);
    }

    [Fact]
    public async Task Handle_ActiveCapabilityEndpointMoved_IsLeftAloneAndFlagged()
    {
        var api = SetupExistingApi();
        var live = new ToolDefinition(Guid.NewGuid(), _tenantId, "getStatus", ToolType.Http, null);
        live.LinkApi(api.Id, "GET", "/status");
        live.Activate();
        SetupCapabilities(live);
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/v2/status", "getStatus", null));

        var result = await Discover();

        Assert.Equal(1, result.CapabilitiesNeedingReview);
        Assert.Equal("/status", live.EndpointPath);
        Assert.Equal(["getStatus"], result.StaleCapabilityNames);
    }

    [Fact]
    public async Task Handle_OperationRemovedFromSpec_ReportedStaleNotDeleted()
    {
        var api = SetupExistingApi();
        var gone = new ToolDefinition(Guid.NewGuid(), _tenantId, "deleteRecord", ToolType.Http, null);
        gone.LinkApi(api.Id, "DELETE", "/records/{id}");
        SetupCapabilities(gone);
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/status", "getStatus", null));

        var result = await Discover();

        Assert.Equal(1, result.StaleCapabilityCount);
        Assert.Equal(["deleteRecord"], result.StaleCapabilityNames);
        _capabilityRepoMock.Verify(r => r.Delete(It.IsAny<ToolDefinition>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SpecWithoutServers_KeepsConfiguredBaseUrl()
    {
        var api = SetupExistingApi("https://configured.example.com");
        SetupSpec(string.Empty, new OpenApiOperationCandidate("GET", "/status", "getStatus", null));

        var result = await Discover();

        Assert.Equal("https://configured.example.com", api.BaseUrl);
        Assert.Equal("https://configured.example.com", result.BaseUrl);
    }

    [Fact]
    public async Task Handle_PublishedApplication_KeepsPublishedStatus()
    {
        _application.Publish();
        SetupSpec("https://api.example.com", new OpenApiOperationCandidate("GET", "/status", "getStatus", null));

        await Discover();

        Assert.Equal(ApplicationStatus.Published, _application.Status);
    }

    [Fact]
    public void Validator_OversizedFileContent_Fails()
    {
        var result = new DiscoverApplicationCommandValidator().Validate(new DiscoverApplicationCommand
        {
            ApplicationId = Guid.NewGuid(),
            OpenApiFileContent = new string('x', 5 * 1024 * 1024 + 1)
        });

        Assert.False(result.IsValid);
    }
}
