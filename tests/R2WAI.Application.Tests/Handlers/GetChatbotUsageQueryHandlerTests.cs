using Moq;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Chatbots.Queries;

namespace R2WAI.Application.Tests.Handlers;

public class GetChatbotUsageQueryHandlerTests
{
    private readonly Mock<IRepository<Chatbot>> _repoMock = new();
    private readonly Mock<IAiUsagePolicyService> _usagePolicyMock = new();

    [Fact]
    public async Task Handle_ExistingChatbot_CombinesLifetimeCountAndTenantPolicyStatus()
    {
        var chatbot = new Chatbot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Bot");
        // TotalMessagesServed is only ever bumped via ExecuteUpdateAsync in production (see
        // ChatbotsController.IncrementMessagesServedAsync) — no domain setter exists to call here,
        // so this test proves the handler reads whatever the repo hands back, not a specific value.
        _repoMock.Setup(r => r.GetByIdAsync(chatbot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(chatbot);
        _usagePolicyMock.Setup(u => u.GetStatusAsync(chatbot.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiUsageStatus(100, 37));

        var handler = new GetChatbotUsageQueryHandler(_repoMock.Object, _usagePolicyMock.Object);
        var result = await handler.Handle(new GetChatbotUsageQuery { ChatbotId = chatbot.Id }, CancellationToken.None);

        Assert.Equal(chatbot.TotalMessagesServed, result.TotalMessagesServed);
        Assert.Equal(100, result.TenantDailyCap);
        Assert.Equal(37, result.TenantDailyUsed);
        // Same rationale as TotalMessagesServed above — PositiveFeedbackCount/NegativeFeedbackCount
        // are only ever bumped via ExecuteUpdateAsync in ChatbotsController.SubmitFeedback.
        Assert.Equal(chatbot.PositiveFeedbackCount, result.PositiveFeedbackCount);
        Assert.Equal(chatbot.NegativeFeedbackCount, result.NegativeFeedbackCount);
    }

    [Fact]
    public async Task Handle_UnknownChatbot_ThrowsNotFound()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Chatbot?)null);

        var handler = new GetChatbotUsageQueryHandler(_repoMock.Object, _usagePolicyMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetChatbotUsageQuery { ChatbotId = Guid.NewGuid() }, CancellationToken.None));
    }
}
