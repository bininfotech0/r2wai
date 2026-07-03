namespace R2WAI.Application.Features.Chatbots.Queries;

public record GetWebhookKeyInfoQuery : IRequest<WebhookKeyInfoDto>
{
    public Guid ChatbotId { get; init; }
    public string WebhookUrl { get; init; } = string.Empty;
}

public class GetWebhookKeyInfoQueryHandler(
    IRepository<Chatbot> chatbotRepo) : IRequestHandler<GetWebhookKeyInfoQuery, WebhookKeyInfoDto>
{
    public async Task<WebhookKeyInfoDto> Handle(GetWebhookKeyInfoQuery query, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(query.ChatbotId, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), query.ChatbotId);

        return new WebhookKeyInfoDto
        {
            WebhookUrl = query.WebhookUrl,
            ApiKeyPrefix = chatbot.WebhookApiKeyPrefix,
            HasKey = !string.IsNullOrEmpty(chatbot.WebhookApiKeyHash),
        };
    }
}
