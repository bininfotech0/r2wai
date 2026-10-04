using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Application.Features.Chatbots.Queries;

// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.1 #54 — "widget volume and quota visibility". No
// Conversation/Message rows exist for the anonymous chatbot chat paths (Chat/StreamChat/Webhook
// reply and return, nothing is persisted per-turn), so TotalMessagesServed is a new lifetime
// counter rather than a COUNT(*) over an existing table — see Chatbot.TotalMessagesServed.
public record GetChatbotUsageQuery : IRequest<ChatbotUsageDto>
{
    public Guid ChatbotId { get; init; }
}

public class GetChatbotUsageQueryHandler(
    IRepository<Chatbot> chatbotRepo,
    IAiUsagePolicyService aiUsagePolicyService) : IRequestHandler<GetChatbotUsageQuery, ChatbotUsageDto>
{
    public async Task<ChatbotUsageDto> Handle(GetChatbotUsageQuery query, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(query.ChatbotId, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), query.ChatbotId);

        var status = await aiUsagePolicyService.GetStatusAsync(chatbot.TenantId, cancellationToken);

        return new ChatbotUsageDto
        {
            TotalMessagesServed = chatbot.TotalMessagesServed,
            TenantDailyCap = status.DailyCap,
            TenantDailyUsed = status.CurrentDailyCount,
            PositiveFeedbackCount = chatbot.PositiveFeedbackCount,
            NegativeFeedbackCount = chatbot.NegativeFeedbackCount,
        };
    }
}
