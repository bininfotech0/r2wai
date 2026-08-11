using AutoMapper;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

public class CreateApplicationVersionCommandHandlerTests
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

    public CreateApplicationVersionCommandHandlerTests()
    {
        _application = new ConnectedApplication(
            Guid.NewGuid(), _tenantId, Guid.NewGuid(), "Property Tax", "PROP");

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _applicationRepoMock.Setup(r => r.GetByIdAsync(_application.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_application);
        _apiRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationApi, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationApi>)[]);
        _configRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationConfiguration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationConfiguration?)null);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ApplicationVersion>)[]);
    }

    private CreateApplicationVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _apiRepoMock.Object, _configRepoMock.Object, _applicationRepoMock.Object,
            _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesFirstVersion()
    {
        var handler = CreateHandler();
        ApplicationVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<ApplicationVersion>(), It.IsAny<CancellationToken>()))
            .Callback<ApplicationVersion, CancellationToken>((v, _) => captured = v)
            .Returns<ApplicationVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new CreateApplicationVersionCommand { ApplicationId = _application.Id, Publish = true };

        await handler.Handle(command, CancellationToken.None);

        _versionRepoMock.Verify(r => r.AddAsync(It.IsAny<ApplicationVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.VersionNumber);
        Assert.True(captured.IsPublished);
        Assert.Contains("Property Tax", captured.ConfigSnapshot);
    }

    [Fact]
    public async Task Handle_MissingTenant_ThrowsUnauthorized()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns((Guid?)null);
        var handler = CreateHandler();

        var command = new CreateApplicationVersionCommand { ApplicationId = _application.Id };

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ApplicationNotFound_ThrowsNotFound()
    {
        _applicationRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConnectedApplication?)null);
        var handler = CreateHandler();

        var command = new CreateApplicationVersionCommand { ApplicationId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
