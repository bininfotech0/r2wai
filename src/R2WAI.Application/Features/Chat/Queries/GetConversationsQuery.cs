namespace R2WAI.Application.Features.Chat.Queries;

public record GetConversationsQuery : IRequest<PagedResult<ConversationDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Module { get; init; }
}

public class GetConversationsQueryHandler(
    IRepository<Conversation> conversationRepo,
    IRepository<AssistantDefinition> assistantRepo,
    IRepository<User> userRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetConversationsQuery, PagedResult<ConversationDto>>
{
    public async Task<PagedResult<ConversationDto>> Handle(GetConversationsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        // Filter at DB level: only the requesting user's non-deleted, non-archived conversations
        System.Linq.Expressions.Expression<Func<Conversation, bool>> filter =
            c => c.TenantId == tenantId
              && c.UserId == userId
              && !c.IsDeleted
              && !c.IsArchived
              && (query.Module == null || c.Module == query.Module);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var result = await conversationRepo.FindPagedAsync(
            filter,
            c => c.ModifiedAt ?? c.CreatedAt,
            page,
            pageSize,
            descending: true,
            cancellationToken: cancellationToken);
        var items = result.Items;

        var dtos = mapper.Map<List<ConversationDto>>(items);

        var currentUserEntity = await userRepo.GetByIdAsync(userId, cancellationToken);
        var userName = currentUserEntity is null ? null : $"{currentUserEntity.FirstName} {currentUserEntity.LastName}".Trim();

        var assistantIds = items
            .Where(c => c.Module == "assistant" && c.ReferenceId.HasValue)
            .Select(c => c.ReferenceId!.Value)
            .Distinct()
            .ToList();
        var assistantNames = assistantIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await assistantRepo.FindAsync(a => assistantIds.Contains(a.Id), cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        var referenceById = items.ToDictionary(c => c.Id, c => (c.Module, c.ReferenceId));

        foreach (var dto in dtos)
        {
            dto.UserName = userName;
            if (referenceById.TryGetValue(dto.Id, out var refInfo) && refInfo.Module == "assistant" && refInfo.ReferenceId.HasValue
                && assistantNames.TryGetValue(refInfo.ReferenceId.Value, out var assistantName))
            {
                dto.AssistantName = assistantName;
            }
        }

        return new PagedResult<ConversationDto>
        {
            Items = dtos,
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
