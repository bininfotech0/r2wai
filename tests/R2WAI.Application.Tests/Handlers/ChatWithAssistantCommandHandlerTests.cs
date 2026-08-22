using Microsoft.Extensions.Logging;
using Moq;

namespace R2WAI.Application.Tests.Handlers;

/// <summary>
/// Covers the fix for a real bug found during the R2WAI redesign: when an assistant has no explicit
/// SystemPrompt, this handler used to fall back to a hardcoded "You are a helpful AI assistant."
/// string instead of the tenant's Prompt Management template for that assistant's type — silently
/// ignoring Phase 5's IPromptTemplateService for every assistant that relied on the type default.
/// </summary>
public class ChatWithAssistantCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssistantWithoutSystemPrompt_UsesPromptTemplateService_NotHardcodedFallback()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assistant = new AssistantDefinition(Guid.NewGuid(), tenantId, "HR Bot", AssistantType.HR);

        var assistantRepoMock = new Mock<IRepository<AssistantDefinition>>();
        assistantRepoMock.Setup(r => r.GetByIdAsync(assistant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assistant);

        var conversationRepoMock = new Mock<IRepository<Conversation>>();
        var messageRepoMock = new Mock<IRepository<Message>>();
        var kbRepoMock = new Mock<IRepository<KnowledgeBase>>();
        var knowledgeBaseServiceMock = new Mock<IKnowledgeBaseService>();

        string? capturedSystemPrompt = null;
        var aiServiceMock = new Mock<IAIService>();
        aiServiceMock
            .Setup(a => a.ChatAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, bool, CancellationToken>((_, _, sysPrompt, _, _) => capturedSystemPrompt = sysPrompt)
            .ReturnsAsync("AI reply");

        var promptTemplateServiceMock = new Mock<IPromptTemplateService>();
        promptTemplateServiceMock
            .Setup(p => p.GetActiveTemplateAsync(AssistantType.HR, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Custom HR template text");

        var traceCollectorMock = new Mock<IChatTraceCollector>();
        traceCollectorMock.Setup(t => t.GetTrace()).Returns(new List<FunctionCallTraceDto>());

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(c => c.TenantId).Returns(tenantId);
        currentUserMock.Setup(c => c.UserId).Returns(userId);

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ChatWithAssistantCommandHandler>>();

        var handler = new ChatWithAssistantCommandHandler(
            assistantRepoMock.Object, conversationRepoMock.Object, messageRepoMock.Object, kbRepoMock.Object,
            knowledgeBaseServiceMock.Object, aiServiceMock.Object, promptTemplateServiceMock.Object,
            traceCollectorMock.Object, currentUserMock.Object, uowMock.Object, loggerMock.Object);

        var command = new ChatWithAssistantCommand { AssistantId = assistant.Id, Message = "Hello" };
        await handler.Handle(command, CancellationToken.None);

        promptTemplateServiceMock.Verify(
            p => p.GetActiveTemplateAsync(AssistantType.HR, tenantId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(capturedSystemPrompt);
        Assert.Contains("Custom HR template text", capturedSystemPrompt);
        Assert.DoesNotContain("You are a helpful AI assistant.", capturedSystemPrompt);
    }

    [Fact]
    public async Task Handle_AssistantWithExplicitSystemPrompt_DoesNotCallPromptTemplateService()
    {
        var tenantId = Guid.NewGuid();
        var assistant = new AssistantDefinition(Guid.NewGuid(), tenantId, "Custom Bot", AssistantType.General);
        assistant.UpdateDetails("Custom Bot", null, "You are a very specific custom assistant.", null, null);

        var assistantRepoMock = new Mock<IRepository<AssistantDefinition>>();
        assistantRepoMock.Setup(r => r.GetByIdAsync(assistant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assistant);

        var conversationRepoMock = new Mock<IRepository<Conversation>>();
        var messageRepoMock = new Mock<IRepository<Message>>();
        var kbRepoMock = new Mock<IRepository<KnowledgeBase>>();
        var knowledgeBaseServiceMock = new Mock<IKnowledgeBaseService>();

        string? capturedSystemPrompt = null;
        var aiServiceMock = new Mock<IAIService>();
        aiServiceMock
            .Setup(a => a.ChatAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, string?, bool, CancellationToken>((_, _, sysPrompt, _, _) => capturedSystemPrompt = sysPrompt)
            .ReturnsAsync("AI reply");

        var promptTemplateServiceMock = new Mock<IPromptTemplateService>();

        var traceCollectorMock = new Mock<IChatTraceCollector>();
        traceCollectorMock.Setup(t => t.GetTrace()).Returns(new List<FunctionCallTraceDto>());

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(c => c.TenantId).Returns(tenantId);
        currentUserMock.Setup(c => c.UserId).Returns(Guid.NewGuid());

        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<ChatWithAssistantCommandHandler>>();

        var handler = new ChatWithAssistantCommandHandler(
            assistantRepoMock.Object, conversationRepoMock.Object, messageRepoMock.Object, kbRepoMock.Object,
            knowledgeBaseServiceMock.Object, aiServiceMock.Object, promptTemplateServiceMock.Object,
            traceCollectorMock.Object, currentUserMock.Object, uowMock.Object, loggerMock.Object);

        var command = new ChatWithAssistantCommand { AssistantId = assistant.Id, Message = "Hello" };
        await handler.Handle(command, CancellationToken.None);

        promptTemplateServiceMock.Verify(
            p => p.GetActiveTemplateAsync(It.IsAny<AssistantType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("You are a very specific custom assistant.", capturedSystemPrompt);
    }
}
