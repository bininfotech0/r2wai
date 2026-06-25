namespace R2WAI.Application.Features.Chat.Queries;

public record GetConversationsQuery : IRequest<PagedResult<ConversationDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Module { get; init; }
}

public class GetConversationsQueryHandler(
    IRepository<Conversation> conversationRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetConversationsQuery, PagedResult<ConversationDto>>
{
    public async Task<PagedResult<ConversationDto>> Handle(GetConversationsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        // Filter at DB level: only the requesting user's non-deleted, non-archived conversations
        var conversations = await conversationRepo.FindAsync(
            c => c.TenantId == tenantId
              && c.UserId == userId
              && !c.IsDeleted
              && !c.IsArchived
              && (query.Module == null || c.Module == query.Module),
            cancellationToken);

        var ordered = conversations
            .OrderByDescending(c => c.ModifiedAt ?? c.CreatedAt);

        var total = ordered.Count();
        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<ConversationDto>
        {
            Items = mapper.Map<List<ConversationDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
