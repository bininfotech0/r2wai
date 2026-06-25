namespace R2WAI.Application.Features.Chat.Queries;

public record GetConversationByIdQuery : IRequest<ConversationDto>
{
    public Guid Id { get; init; }
}

public class GetConversationByIdQueryHandler(
    IRepository<Conversation> conversationRepo,
    IMapper mapper) : IRequestHandler<GetConversationByIdQuery, ConversationDto>
{
    public async Task<ConversationDto> Handle(GetConversationByIdQuery query, CancellationToken cancellationToken)
    {
        var conversation = await conversationRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), query.Id);

        if (conversation.IsDeleted)
            throw new NotFoundException(nameof(Conversation), query.Id);

        return mapper.Map<ConversationDto>(conversation);
    }
}
