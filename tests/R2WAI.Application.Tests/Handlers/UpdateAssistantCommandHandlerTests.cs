using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Assistants.Commands;
using R2WAI.Application.Features.Assistants.Mappings;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Domain.Interfaces;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// docs/api/MISSING-BACKEND-ENDPOINTS.md — the KnowledgeBase "can't detach once linked" gap.
/// `Guid?` can't distinguish "field omitted" from "explicit null" once JSON-bound, so unlinking
/// needed its own explicit flag (`UnlinkKnowledgeBase`) rather than overloading a null
/// `KnowledgeBaseId`. These tests prove all three states the handler must tell apart.
/// </summary>
public class UpdateAssistantCommandHandlerTests
{
    private readonly Mock<IRepository<AssistantDefinition>> _assistantRepoMock = new();
    private readonly Mock<IRepository<AssistantPromptHistory>> _historyRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly IMapper _mapper;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateAssistantCommandHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<AssistantProfile>(), NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
        _currentUserMock.Setup(c => c.TenantId).Returns(_tenantId);
        _historyRepoMock.Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<AssistantPromptHistory, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private UpdateAssistantCommandHandler CreateHandler() =>
        new(_assistantRepoMock.Object, _historyRepoMock.Object, _unitOfWorkMock.Object,
            _currentUserMock.Object, _cacheMock.Object, _mapper);

    private AssistantDefinition SeedAssistant(Guid? knowledgeBaseId = null, Guid? modelConfigurationId = null)
    {
        var assistant = new AssistantDefinition(Guid.NewGuid(), _tenantId, "Existing", AssistantType.General,
            modelConfigurationId: modelConfigurationId, knowledgeBaseId: knowledgeBaseId);
        _assistantRepoMock.Setup(r => r.GetByIdAsync(assistant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assistant);
        return assistant;
    }

    [Fact]
    public async Task Handle_KnowledgeBaseIdProvided_LinksIt()
    {
        var assistant = SeedAssistant(knowledgeBaseId: null);
        var kbId = Guid.NewGuid();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateAssistantCommand { Id = assistant.Id, Name = "Existing", KnowledgeBaseId = kbId },
            CancellationToken.None);

        Assert.Equal(kbId, assistant.KnowledgeBaseId);
        Assert.Equal(kbId, result.KnowledgeBaseId);
    }

    [Fact]
    public async Task Handle_UnlinkKnowledgeBaseTrue_ClearsIt()
    {
        var assistant = SeedAssistant(knowledgeBaseId: Guid.NewGuid());
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateAssistantCommand { Id = assistant.Id, Name = "Existing", UnlinkKnowledgeBase = true },
            CancellationToken.None);

        Assert.Null(assistant.KnowledgeBaseId);
        Assert.Null(result.KnowledgeBaseId);
    }

    [Fact]
    public async Task Handle_KnowledgeBaseIdOmitted_PreservesExistingValue()
    {
        // The real regression this guards: CreateAssistantDialog.tsx calls this endpoint with a
        // hand-built partial payload that never mentions knowledgeBaseId at all — that omission
        // must never be read as "clear it".
        var kbId = Guid.NewGuid();
        var assistant = SeedAssistant(knowledgeBaseId: kbId);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateAssistantCommand { Id = assistant.Id, Name = "Existing", Description = "partial update" },
            CancellationToken.None);

        Assert.Equal(kbId, assistant.KnowledgeBaseId);
        Assert.Equal(kbId, result.KnowledgeBaseId);
    }

    [Fact]
    public async Task Handle_UnlinkTrueAndKnowledgeBaseIdBothSet_UnlinkWins()
    {
        // UnlinkKnowledgeBase is checked first in the handler — an inconsistent request (both a
        // target KB and the unlink flag) must not silently link instead of unlinking.
        var assistant = SeedAssistant(knowledgeBaseId: Guid.NewGuid());
        var handler = CreateHandler();

        await handler.Handle(
            new UpdateAssistantCommand { Id = assistant.Id, Name = "Existing", KnowledgeBaseId = Guid.NewGuid(), UnlinkKnowledgeBase = true },
            CancellationToken.None);

        Assert.Null(assistant.KnowledgeBaseId);
    }

    [Fact]
    public async Task Handle_UnlinkModelConfigurationTrue_ClearsIt()
    {
        // The live bug this guards: AssistantStudioPage's "Use tenant default" model option
        // (value "") serialized to modelConfigurationId: undefined — omitted, so the old
        // `if (ModelConfigurationId.HasValue)` guard never fired and the existing model config
        // was never actually cleared, even though the control looked like it worked.
        var assistant = SeedAssistant(modelConfigurationId: Guid.NewGuid());
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateAssistantCommand { Id = assistant.Id, Name = "Existing", UnlinkModelConfiguration = true },
            CancellationToken.None);

        Assert.Null(assistant.ModelConfigurationId);
        Assert.Null(result.ModelConfigurationId);
    }

    [Fact]
    public async Task Handle_ModelConfigurationIdOmitted_PreservesExistingValue()
    {
        var modelId = Guid.NewGuid();
        var assistant = SeedAssistant(modelConfigurationId: modelId);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new UpdateAssistantCommand { Id = assistant.Id, Name = "Existing", Description = "partial update" },
            CancellationToken.None);

        Assert.Equal(modelId, assistant.ModelConfigurationId);
        Assert.Equal(modelId, result.ModelConfigurationId);
    }
}
