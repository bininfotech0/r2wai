using AutoMapper;
using Moq;
using R2WAI.Application.Features.Assistants.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class RollbackAssistantVersionCommandHandlerTests
{
    private readonly Mock<IRepository<AssistantVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<AssistantDefinition>> _assistantRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly AssistantDefinition _assistant;
    private readonly AssistantVersion _targetVersion;

    public RollbackAssistantVersionCommandHandlerTests()
    {
        _assistant = new AssistantDefinition(Guid.NewGuid(), _tenantId, "Current Name", AssistantType.HR);
        _assistant.UpdateDetails("Current Name", "current desc", "current prompt", "[]", null);

        const string snapshotJson =
            """{"Name":"Old Name","Description":"old desc","Type":1,"SystemPrompt":"old prompt","ModelConfigurationId":null,"KnowledgeBaseId":null,"Tools":null,"Settings":null,"Tags":"old-tag","AvatarUrl":null}""";

        _targetVersion = AssistantVersion.CreateSnapshot(
            Guid.NewGuid(), _tenantId, _assistant.Id, 1, snapshotJson);

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _versionRepoMock.Setup(r => r.GetByIdAsync(_targetVersion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_targetVersion);
        _assistantRepoMock.Setup(r => r.GetByIdAsync(_assistant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_assistant);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AssistantVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AssistantVersion>)[_targetVersion]);
    }

    private RollbackAssistantVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _assistantRepoMock.Object, _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_RestoresDetailsAndPublishesNewVersion()
    {
        var handler = CreateHandler();
        AssistantVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<AssistantVersion>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantVersion, CancellationToken>((v, _) => captured = v)
            .Returns<AssistantVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new RollbackAssistantVersionCommand { VersionId = _targetVersion.Id };

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Old Name", _assistant.Name);
        Assert.Equal("old desc", _assistant.Description);
        Assert.Equal("old prompt", _assistant.SystemPrompt);
        Assert.Equal("old-tag", _assistant.Tags);
        // Type is never restored — no setter exists (see AssistantVersionSnapshotService's comment).
        Assert.Equal(AssistantType.HR, _assistant.Type);

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
            .ReturnsAsync((AssistantVersion?)null);
        var handler = CreateHandler();

        var command = new RollbackAssistantVersionCommand { VersionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
