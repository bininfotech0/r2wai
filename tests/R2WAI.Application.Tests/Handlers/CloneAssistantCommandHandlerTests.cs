using AutoMapper;
using Moq;
using R2WAI.Application.Features.Assistants.Commands;

namespace R2WAI.Application.Tests.Handlers;

public class CloneAssistantCommandHandlerTests
{
    private readonly Mock<IRepository<AssistantDefinition>> _assistantRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IMapper> _mapperMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly AssistantDefinition _source;

    public CloneAssistantCommandHandlerTests()
    {
        _source = new AssistantDefinition(Guid.NewGuid(), _tenantId, "HR Bot", AssistantType.HR,
            modelConfigurationId: Guid.NewGuid(), knowledgeBaseId: Guid.NewGuid());
        _source.UpdateDetails("HR Bot", "Answers HR questions", "You are an HR assistant.",
            "[\"tool-1\"]", "{\"tone\":\"Friendly\"}", "hr,onboarding", "https://example.com/avatar.png");
        _source.Publish(); // published source — the clone must not inherit this

        _currentUserMock.Setup(x => x.TenantId).Returns(_tenantId);
        _assistantRepoMock.Setup(r => r.GetByIdAsync(_source.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_source);
    }

    private CloneAssistantCommandHandler CreateHandler() =>
        new(_assistantRepoMock.Object, _uowMock.Object, _currentUserMock.Object, _cacheMock.Object, _mapperMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_CreatesDraftCopyWithSourceConfigButNotPublishStatus()
    {
        var handler = CreateHandler();
        AssistantDefinition? captured = null;
        _assistantRepoMock.Setup(r => r.AddAsync(It.IsAny<AssistantDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantDefinition, CancellationToken>((a, _) => captured = a)
            .Returns<AssistantDefinition, CancellationToken>((a, _) => Task.FromResult(a));

        var command = new CloneAssistantCommand { AssistantDefinitionId = _source.Id };
        await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.NotEqual(_source.Id, captured!.Id);
        Assert.Equal("HR Bot (Copy)", captured.Name);
        Assert.Equal("Answers HR questions", captured.Description);
        Assert.Equal(AssistantType.HR, captured.Type);
        Assert.Equal("You are an HR assistant.", captured.SystemPrompt);
        Assert.Equal(_source.ModelConfigurationId, captured.ModelConfigurationId);
        Assert.Equal(_source.KnowledgeBaseId, captured.KnowledgeBaseId);
        Assert.Equal("[\"tool-1\"]", captured.Tools);
        Assert.Equal("{\"tone\":\"Friendly\"}", captured.Settings);
        Assert.Equal("hr,onboarding", captured.Tags);
        Assert.Equal("https://example.com/avatar.png", captured.AvatarUrl);

        // The whole point of the test: a clone of a published assistant must start as a fresh,
        // unpublished draft — never silently publish a second live assistant.
        Assert.False(captured.IsActive);
        Assert.Equal(PublishStatus.Draft, captured.PublishStatus);
        Assert.Equal(0, captured.PublishedVersion);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SourceNotFound_ThrowsNotFound()
    {
        _assistantRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssistantDefinition?)null);
        var handler = CreateHandler();

        var command = new CloneAssistantCommand { AssistantDefinitionId = Guid.NewGuid() };

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MissingTenant_ThrowsUnauthorized()
    {
        _currentUserMock.Setup(x => x.TenantId).Returns((Guid?)null);
        var handler = CreateHandler();

        var command = new CloneAssistantCommand { AssistantDefinitionId = _source.Id };

        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(command, CancellationToken.None));
    }
}
