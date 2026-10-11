using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

public class RollbackApplicationVersionCommandHandlerTests
{
    private readonly Mock<IRepository<ApplicationVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<ApplicationApi>> _apiRepoMock = new();
    private readonly Mock<IRepository<ApplicationConfiguration>> _configRepoMock = new();
    private readonly Mock<IRepository<ConnectedApplication>> _applicationRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ConnectedApplication _application;
    private readonly ApplicationVersion _targetVersion;

    public RollbackApplicationVersionCommandHandlerTests()
    {
        _application = new ConnectedApplication(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), "Property Tax Portal", "PROP",
            "Current description", "https://current.example.gov");

        const string snapshotJson =
            """{"Name":"Property Tax","Description":null,"BaseUrl":"https://tax.example.gov","Environment":"Production","Apis":[],"Configuration":null}""";

        _targetVersion = ApplicationVersion.CreateSnapshot(
            Guid.NewGuid(), _tenantId, _application.Id, 1, snapshotJson);

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _versionRepoMock.Setup(r => r.GetByIdAsync(_targetVersion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_targetVersion);
        _applicationRepoMock.Setup(r => r.GetByIdAsync(_application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_application);
        _apiRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationApi, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationApi>)[]);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationVersion>)[_targetVersion]);
    }

    private RollbackApplicationVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _apiRepoMock.Object, _configRepoMock.Object, _applicationRepoMock.Object,
            _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_RestoresApplicationDetailsAndPublishesNewVersion()
    {
        var handler = CreateHandler();
        ApplicationVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<ApplicationVersion>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationVersion, CancellationToken>((v, _) => captured = v)
            .Returns<ApplicationVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new RollbackApplicationVersionCommand { VersionId = _targetVersion.Id };

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Property Tax", _application.Name);
        Assert.Equal("https://tax.example.gov", _application.BaseUrl);
        Assert.Equal(ApplicationEnvironment.Production, _application.Environment);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.VersionNumber);
        Assert.True(captured.IsPublished);
        Assert.Equal("Rolled back to v1", captured.Note);
        Assert.Equal(_targetVersion.ConfigSnapshot, captured.ConfigSnapshot);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ApiInSnapshot_IsUpdatedInPlaceNotRecreated()
    {
        // Recreating the API gave it a new id, which unlinked every discovered capability (FK
        // SetNull) and dropped its encrypted credential, which snapshots deliberately omit.
        var api = new ApplicationApi(Guid.NewGuid(), _tenantId, _application.Id, "Tax API", "https://new.example.gov");
        api.SetCredential("encrypted-secret", "X-Api-Key");
        var removedApi = new ApplicationApi(Guid.NewGuid(), _tenantId, _application.Id, "Added Later", "https://later.example.gov");
        _apiRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationApi, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationApi>)[api, removedApi]);

        const string snapshotJson =
            """{"Name":"Property Tax","Apis":[{"Name":"tax api","BaseUrl":"https://old.example.gov","AuthScheme":"ApiKey"}]}""";
        var version = ApplicationVersion.CreateSnapshot(Guid.NewGuid(), _tenantId, _application.Id, 1, snapshotJson);
        _versionRepoMock.Setup(r => r.GetByIdAsync(version.Id, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationVersion>)[version]);

        await CreateHandler().Handle(new RollbackApplicationVersionCommand { VersionId = version.Id }, CancellationToken.None);

        Assert.Equal("https://old.example.gov", api.BaseUrl);
        Assert.Equal(ApiAuthScheme.ApiKey, api.AuthScheme);
        Assert.Equal("encrypted-secret", api.CredentialSecretEncrypted);
        _apiRepoMock.Verify(r => r.Delete(api), Times.Never);
        _apiRepoMock.Verify(r => r.Delete(removedApi), Times.Once);
        _apiRepoMock.Verify(r => r.AddAsync(It.IsAny<ApplicationApi>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_VersionNotFound_ThrowsNotFound()
    {
        _versionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationVersion?)null);
        var handler = CreateHandler();

        var command = new RollbackApplicationVersionCommand { VersionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
