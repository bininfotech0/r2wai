namespace R2WAI.Application.Features.Chat.Queries;

public record GetMessagesQuery : IRequest<PagedResult<MessageDto>>
{
    public Guid ConversationId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetMessagesQueryHandler(
    IRepository<Conversation> conversationRepo,
    IRepository<Message> messageRepo,
    IMapper mapper) : IRequestHandler<GetMessagesQuery, PagedResult<MessageDto>>
{
    public async Task<PagedResult<MessageDto>> Handle(GetMessagesQuery query, CancellationToken cancellationToken)
    {
        var conversation = await conversationRepo.GetByIdAsync(query.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), query.ConversationId);

        if (conversation.IsDeleted)
            throw new NotFoundException(nameof(Conversation), query.ConversationId);

        // Filter at DB level — do not load all messages into memory
        var messages = await messageRepo.FindAsync(
            m => m.ConversationId == query.ConversationId && !m.IsDeleted,
            cancellationToken);

        var ordered = messages.OrderBy(m => m.CreatedAt);

        var total = ordered.Count();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<MessageDto>
        {
            Items = mapper.Map<List<MessageDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
