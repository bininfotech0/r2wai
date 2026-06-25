namespace R2WAI.Application.Features.Chatbots.Queries;

public record GetChatbotByIdQuery : IRequest<ChatbotDto>
{
    public Guid Id { get; init; }
}

public class GetChatbotByIdQueryHandler(
    IRepository<Chatbot> chatbotRepo,
    IMapper mapper) : IRequestHandler<GetChatbotByIdQuery, ChatbotDto>
{
    public async Task<ChatbotDto> Handle(GetChatbotByIdQuery query, CancellationToken cancellationToken)
    {
        var chatbot = await chatbotRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Chatbot), query.Id);

        if (chatbot.IsDeleted)
            throw new NotFoundException(nameof(Chatbot), query.Id);

        return mapper.Map<ChatbotDto>(chatbot);
    }
}
