using Microsoft.Extensions.Logging;
using Moq;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Features.Assistants.Commands;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// Implementation plan Phase 1 — the standalone "publish an assistant as its own REST endpoint"
/// path. The whole point is that this resolves config from the *published version's snapshot*,
/// never the live AssistantDefinition, so an edit made after publishing can't silently change what
/// an already-published caller gets — these tests prove that directly, not just that the handler
/// runs.
/// </summary>
public class ChatWithPublishedAssistantCommandHandlerTests
{
    private static (Mock<IRepository<AssistantDefinition>> AssistantRepo, Mock<IRepository<AssistantVersion>> VersionRepo,
        Mock<IAIService> AiService, ChatWithPublishedAssistantCommandHandler Handler, Action<string?> CapturePrompt,
        Func<Guid?> CapturedModelConfigId) BuildHandler(
        AssistantDefinition assistant, AssistantVersion? publishedVersion, Guid tenantId, Guid userId)
    {
        var assistantRepoMock = new Mock<IRepository<AssistantDefinition>>();
        assistantRepoMock.Setup(r => r.GetByIdAsync(assistant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assistant);

        var versionRepoMock = new Mock<IRepository<AssistantVersion>>();
        versionRepoMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<AssistantVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(publishedVersion is null
                ? (IReadOnlyList<AssistantVersion>)[]
                : [publishedVersion]);

        var conversationRepoMock = new Mock<IRepository<Conversation>>();
        var messageRepoMock = new Mock<IRepository<Message>>();
        var knowledgeBaseServiceMock = new Mock<IKnowledgeBaseService>();
        var agenticRetrievalMock = new Mock<IAgenticRetrievalOrchestrator>();

        string? capturedSystemPrompt = null;
        var aiServiceMock = new Mock<IAIService>();
        aiServiceMock
            .Setup(a => a.ChatAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<ResolvedModelConfig?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, bool, ResolvedModelConfig?, IReadOnlyCollection<Guid>?, CancellationToken>((_, _, sysPrompt, _, _, _, _) => capturedSystemPrompt = sysPrompt)
            .ReturnsAsync("AI reply");

        var promptTemplateServiceMock = new Mock<IPromptTemplateService>();
        promptTemplateServiceMock
            .Setup(p => p.GetActiveTemplateAsync(It.IsAny<AssistantType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Fallback template text");

        Guid? capturedModelConfigId = null;
        var modelConfigResolverMock = new Mock<IModelConfigurationResolver>();
        modelConfigResolverMock
            .Setup(m => m.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, Guid, CancellationToken>((modelConfigId, _, _) => capturedModelConfigId = modelConfigId)
            .ReturnsAsync((ResolvedModelConfig?)null);

        var traceCollectorMock = new Mock<IChatTraceCollector>();
        traceCollectorMock.Setup(t => t.GetTrace()).Returns(new List<FunctionCallTraceDto>());

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(c => c.TenantId).Returns(tenantId);
        currentUserMock.Setup(c => c.UserId).Returns(userId);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ChatWithPublishedAssistantCommandHandler>>();
        var aiUsagePolicyServiceMock = new Mock<IAiUsagePolicyService>();
        aiUsagePolicyServiceMock.Setup(p => p.IsUnderCapAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var piiPolicyServiceMock = new Mock<IPiiPolicyService>();
        piiPolicyServiceMock
            .Setup(p => p.CheckAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, Guid _, CancellationToken _) => new PiiCheckResult(false, text, []));
        var auditLogRepoMock = new Mock<IRepository<AuditLog>>();

        var handler = new ChatWithPublishedAssistantCommandHandler(
            assistantRepoMock.Object, versionRepoMock.Object, conversationRepoMock.Object, messageRepoMock.Object,
            knowledgeBaseServiceMock.Object, agenticRetrievalMock.Object, aiServiceMock.Object, promptTemplateServiceMock.Object,
            modelConfigResolverMock.Object, aiUsagePolicyServiceMock.Object, piiPolicyServiceMock.Object, auditLogRepoMock.Object,
            traceCollectorMock.Object, currentUserMock.Object, uowMock.Object, loggerMock.Object);

        return (assistantRepoMock, versionRepoMock, aiServiceMock, handler, s => capturedSystemPrompt = s, () => capturedModelConfigId);
    }

    [Fact]
    public async Task Handle_NoPublishedVersion_ThrowsValidation_NotSilentLiveFallback()
    {
        var tenantId = Guid.NewGuid();
        var assistant = new AssistantDefinition(Guid.NewGuid(), tenantId, "Unpublished Bot", AssistantType.General);

        var (_, _, _, handler, _, _) = BuildHandler(assistant, publishedVersion: null, tenantId, Guid.NewGuid());

        var command = new ChatWithPublishedAssistantCommand { AssistantId = assistant.Id, Message = "Hello" };
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PublishedVersionExists_UsesSnapshotConfig_NotLiveConfig_EvenAfterLiveEdit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var publishedModelConfigId = Guid.NewGuid();
        var liveModelConfigId = Guid.NewGuid();

        var assistant = new AssistantDefinition(Guid.NewGuid(), tenantId, "Support Bot", AssistantType.General);
        assistant.UpdateDetails("Support Bot", null, "Snapshot-era system prompt.", null, null);
        assistant.LinkModelConfiguration(publishedModelConfigId);

        var snapshotJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            Name = "Support Bot",
            Description = (string?)null,
            Type = AssistantType.General,
            SystemPrompt = "Snapshot-era system prompt.",
            ModelConfigurationId = publishedModelConfigId,
            KnowledgeBaseId = (Guid?)null,
            Tools = (string?)null,
            Settings = (string?)null,
            Tags = (string?)null,
            AvatarUrl = (string?)null,
        });
        var publishedVersion = AssistantVersion.CreateSnapshot(Guid.NewGuid(), tenantId, assistant.Id, 1, snapshotJson);
        publishedVersion.Publish(userId);

        // Live edit AFTER the snapshot was taken — the published endpoint must not see this.
        assistant.UpdateDetails("Support Bot", null, "Live edit made after publish — must not be served.", null, null);
        assistant.LinkModelConfiguration(liveModelConfigId);

        var (_, _, aiServiceMock, handler, capturePrompt, capturedModelConfigId) =
            BuildHandler(assistant, publishedVersion, tenantId, userId);

        string? capturedSystemPrompt = null;
        aiServiceMock
            .Setup(a => a.ChatAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<ResolvedModelConfig?>(), It.IsAny<IReadOnlyCollection<Guid>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, bool, ResolvedModelConfig?, IReadOnlyCollection<Guid>?, CancellationToken>((_, _, sysPrompt, _, _, _, _) => capturedSystemPrompt = sysPrompt)
            .ReturnsAsync("AI reply");

        var command = new ChatWithPublishedAssistantCommand { AssistantId = assistant.Id, Message = "Hello" };
        await handler.Handle(command, CancellationToken.None);

        Assert.Contains("Snapshot-era system prompt.", capturedSystemPrompt);
        Assert.DoesNotContain("Live edit made after publish", capturedSystemPrompt);
        Assert.Equal(publishedModelConfigId, capturedModelConfigId());
    }

    [Fact]
    public async Task Handle_AssistantBelongsToAnotherTenant_ThrowsNotFound()
    {
        var assistantTenantId = Guid.NewGuid();
        var callerTenantId = Guid.NewGuid();
        var assistant = new AssistantDefinition(Guid.NewGuid(), assistantTenantId, "Other Tenant's Bot", AssistantType.General);

        var (_, _, _, handler, _, _) = BuildHandler(assistant, publishedVersion: null, callerTenantId, Guid.NewGuid());

        var command = new ChatWithPublishedAssistantCommand { AssistantId = assistant.Id, Message = "Hello" };
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
