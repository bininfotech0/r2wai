using AutoMapper;
using Moq;
using R2WAI.Application.Features.KnowledgeBases.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class CreateKnowledgeBaseVersionCommandHandlerTests
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

    public CreateKnowledgeBaseVersionCommandHandlerTests()
    {
        _knowledgeBase = new KnowledgeBase(Guid.NewGuid(), _tenantId, _userId, "Policy Docs");

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _knowledgeBaseRepoMock.Setup(r => r.GetByIdAsync(_knowledgeBase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_knowledgeBase);
        _sourceRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<KnowledgeBaseSource, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<KnowledgeBaseSource>)[]);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<KnowledgeBaseVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<KnowledgeBaseVersion>)[]);
    }

    private CreateKnowledgeBaseVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _sourceRepoMock.Object, _knowledgeBaseRepoMock.Object,
            _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesFirstVersion()
    {
        var handler = CreateHandler();
        KnowledgeBaseVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<KnowledgeBaseVersion>(), It.IsAny<CancellationToken>()))
            .Callback<KnowledgeBaseVersion, CancellationToken>((v, _) => captured = v)
            .Returns<KnowledgeBaseVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new CreateKnowledgeBaseVersionCommand { KnowledgeBaseId = _knowledgeBase.Id, Publish = true };

        await handler.Handle(command, CancellationToken.None);

        _versionRepoMock.Verify(r => r.AddAsync(It.IsAny<KnowledgeBaseVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.VersionNumber);
        Assert.True(captured.IsPublished);
        Assert.Contains("Policy Docs", captured.ConfigSnapshot);
    }

    [Fact]
    public async Task Handle_MissingTenant_ThrowsUnauthorized()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns((Guid?)null);
        var handler = CreateHandler();

        var command = new CreateKnowledgeBaseVersionCommand { KnowledgeBaseId = _knowledgeBase.Id };

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_KnowledgeBaseNotFound_ThrowsNotFound()
    {
        _knowledgeBaseRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeBase?)null);
        var handler = CreateHandler();

        var command = new CreateKnowledgeBaseVersionCommand { KnowledgeBaseId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
