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
}
