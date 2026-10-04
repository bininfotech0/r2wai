using AutoMapper;
using Moq;
using R2WAI.Application.Features.Assistants.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class CreateAssistantVersionCommandHandlerTests
{
    private readonly Mock<IRepository<AssistantVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<AssistantDefinition>> _assistantRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly AssistantDefinition _assistant;

    public CreateAssistantVersionCommandHandlerTests()
    {
        _assistant = new AssistantDefinition(Guid.NewGuid(), _tenantId, "HR Bot", AssistantType.HR);

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserMock.Setup(x => x.UserId).Returns(_userId);

        _assistantRepoMock.Setup(r => r.GetByIdAsync(_assistant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_assistant);
        _versionRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AssistantVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AssistantVersion>)[]);
    }

    private CreateAssistantVersionCommandHandler CreateHandler() =>
        new(_versionRepoMock.Object, _assistantRepoMock.Object, _uowMock.Object, _currentUserMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesFirstVersion()
    {
        var handler = CreateHandler();
        AssistantVersion? captured = null;
        _versionRepoMock.Setup(r => r.AddAsync(It.IsAny<AssistantVersion>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantVersion, CancellationToken>((v, _) => captured = v)
            .Returns<AssistantVersion, CancellationToken>((v, _) => Task.FromResult(v));

        var command = new CreateAssistantVersionCommand { AssistantDefinitionId = _assistant.Id, Publish = true };

        await handler.Handle(command, CancellationToken.None);

        _versionRepoMock.Verify(r => r.AddAsync(It.IsAny<AssistantVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.VersionNumber);
        Assert.True(captured.IsPublished);
        Assert.Contains("HR Bot", captured.ConfigSnapshot);
    }

    [Fact]
    public async Task Handle_MissingTenant_ThrowsUnauthorized()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns((Guid?)null);
        var handler = CreateHandler();

        var command = new CreateAssistantVersionCommand { AssistantDefinitionId = _assistant.Id };

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AssistantNotFound_ThrowsNotFound()
    {
        _assistantRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantDefinition?)null);
        var handler = CreateHandler();

        var command = new CreateAssistantVersionCommand { AssistantDefinitionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
