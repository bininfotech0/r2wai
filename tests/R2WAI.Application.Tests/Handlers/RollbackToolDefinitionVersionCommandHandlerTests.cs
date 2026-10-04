using AutoMapper;
using Moq;
using R2WAI.Application.Features.Capabilities.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class RollbackToolDefinitionVersionCommandHandlerTests
{
    private readonly Mock<IRepository<ToolDefinitionVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<ToolDefinition>> _toolRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ToolDefinition _tool;
    private readonly ToolDefinitionVersion _targetVersion;

    public RollbackToolDefinitionVersionCommandHandlerTests()
    {
        _tool = new ToolDefinition(Guid.NewGuid(), _tenantId, "Current Name", ToolType.Http, "current desc",
            "https://current.example.com");

        const string snapshotJson =
            """{"Name":"Old Name","Description":"old desc","ToolType":"Http","EndpointUrl":"https://old.example.com","HttpMethod":"GET","EndpointPath":"/status","Configuration":null,"ApplicationApiId":null,"RiskLevel":"Low","RequiredRole":null,"ConfirmationRequired":false,"ApprovalRequired":false,"AuditRequired":true}""";

        _targetVersion = ToolDefinitionVersion.CreateSnapshot(
            Guid.NewGuid(), _tenantId, _tool.Id, 1, snapshotJson);

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _versionRepoMock.Setup(r => r.GetByIdAsync(_targetVersion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_targetVersion);
        _toolRepoMock.Setup(r => r.GetByIdAsync(_tool.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tool);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ToolDefinitionVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ToolDefinitionVersion>)[_targetVersion]);
    }

    private RollbackToolDefinitionVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _toolRepoMock.Object, _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_RestoresToolDetailsAndPublishesNewVersion()
    {
        var handler = CreateHandler();
        ToolDefinitionVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<ToolDefinitionVersion>(), It.IsAny<CancellationToken>()))
            .Callback<ToolDefinitionVersion, CancellationToken>((v, _) => captured = v)
            .Returns<ToolDefinitionVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new RollbackToolDefinitionVersionCommand { VersionId = _targetVersion.Id };

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Old Name", _tool.Name);
        Assert.Equal("https://old.example.com", _tool.EndpointUrl);
        Assert.Equal("GET", _tool.HttpMethod);
        Assert.Equal("/status", _tool.EndpointPath);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.VersionNumber);
        Assert.True(captured.IsPublished);
        Assert.Equal("Rolled back to v1", captured.Note);
        Assert.Equal(_targetVersion.ConfigSnapshot, captured.ConfigSnapshot);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_VersionNotFound_ThrowsNotFound()
    {
        _versionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolDefinitionVersion?)null);
        var handler = CreateHandler();

        var command = new RollbackToolDefinitionVersionCommand { VersionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
