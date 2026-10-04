using AutoMapper;
using Moq;
using R2WAI.Application.Features.KnowledgeBases.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class RollbackKnowledgeBaseVersionCommandHandlerTests
{
    private readonly Mock<IRepository<KnowledgeBaseVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<KnowledgeBaseSource>> _sourceRepoMock = new();
    private readonly Mock<IRepository<KnowledgeBase>> _knowledgeBaseRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly KnowledgeBase _knowledgeBase;
    private readonly KnowledgeBaseVersion _targetVersion;

    public RollbackKnowledgeBaseVersionCommandHandlerTests()
    {
        _knowledgeBase = new KnowledgeBase(Guid.NewGuid(), _tenantId, _userId, "Current Name", "current desc");

        const string snapshotJson =
            """{"Name":"Old Name","Description":"old desc","EmbeddingModel":null,"ChunkSize":null,"ChunkOverlap":null,"VectorCollectionName":null,"Sources":[{"Type":"Url","ReferenceId":null,"Url":"https://old.example.com/doc","Content":null}]}""";

        _targetVersion = KnowledgeBaseVersion.CreateSnapshot(
            Guid.NewGuid(), _tenantId, _knowledgeBase.Id, 1, snapshotJson);

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _versionRepoMock.Setup(r => r.GetByIdAsync(_targetVersion.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_targetVersion);
        _knowledgeBaseRepoMock.Setup(r => r.GetByIdAsync(_knowledgeBase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knowledgeBase);
        _sourceRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<KnowledgeBaseSource, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<KnowledgeBaseSource>)[]);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<KnowledgeBaseVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<KnowledgeBaseVersion>)[_targetVersion]);
    }

    private RollbackKnowledgeBaseVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _sourceRepoMock.Object, _knowledgeBaseRepoMock.Object,
            _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_RestoresDetailsSourcesAndPublishesNewVersion()
    {
        var handler = CreateHandler();
        KnowledgeBaseVersion? capturedVersion = null;
        KnowledgeBaseSource? capturedSource = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<KnowledgeBaseVersion>(), It.IsAny<CancellationToken>()))
            .Callback<KnowledgeBaseVersion, CancellationToken>((v, _) => capturedVersion = v)
            .Returns<KnowledgeBaseVersion, CancellationToken>((v, _) => Task.FromResult(v));
        _sourceRepoMock.Setup(r => r.AddAsync(It.IsAny<KnowledgeBaseSource>(), It.IsAny<CancellationToken>()))
            .Callback<KnowledgeBaseSource, CancellationToken>((s, _) => capturedSource = s)
            .Returns<KnowledgeBaseSource, CancellationToken>((s, _) => Task.FromResult(s));

        var command = new RollbackKnowledgeBaseVersionCommand { VersionId = _targetVersion.Id };

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Old Name", _knowledgeBase.Name);
        Assert.Equal("old desc", _knowledgeBase.Description);

        Assert.NotNull(capturedSource);
        Assert.Equal("Url", capturedSource!.Type);
        Assert.Equal("https://old.example.com/doc", capturedSource.Url);

        Assert.NotNull(capturedVersion);
        Assert.Equal(2, capturedVersion!.VersionNumber);
        Assert.True(capturedVersion.IsPublished);
        Assert.Equal("Rolled back to v1", capturedVersion.Note);
        Assert.Equal(_targetVersion.ConfigSnapshot, capturedVersion.ConfigSnapshot);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_VersionNotFound_ThrowsNotFound()
    {
        _versionRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeBaseVersion?)null);
        var handler = CreateHandler();

        var command = new RollbackKnowledgeBaseVersionCommand { VersionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
