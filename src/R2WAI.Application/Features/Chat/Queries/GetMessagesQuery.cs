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
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetMessagesQuery, PagedResult<MessageDto>>
{
    public async Task<PagedResult<MessageDto>> Handle(GetMessagesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var conversation = await conversationRepo.GetByIdAsync(query.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), query.ConversationId);

        if (conversation.TenantId != tenantId || conversation.UserId != userId)
            throw new NotFoundException(nameof(Conversation), query.ConversationId);

        if (conversation.IsDeleted)
            throw new NotFoundException(nameof(Conversation), query.ConversationId);

        // Apply filtering, ordering, count, and paging in SQL. Loading the full history before
        // Skip/Take made a long conversation consume memory proportional to its entire transcript.
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var result = await messageRepo.FindPagedAsync(
            m => m.ConversationId == query.ConversationId && !m.IsDeleted,
            m => m.CreatedAt,
            page,
            pageSize,
            cancellationToken: cancellationToken);

        return new PagedResult<MessageDto>
        {
            Items = mapper.Map<List<MessageDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
