namespace R2WAI.Application.Features.Members.Queries;

public record GetPlanUpgradeRequestsQuery : IRequest<PagedResult<PlanUpgradeRequestDto>>, IAuthorizedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class GetPlanUpgradeRequestsQueryHandler(
    IRepository<PlanUpgradeRequest> requestRepo,
    IRepository<User> userRepo,
    ICurrentUserService currentUser) : IRequestHandler<GetPlanUpgradeRequestsQuery, PagedResult<PlanUpgradeRequestDto>>
{
    public async Task<PagedResult<PlanUpgradeRequestDto>> Handle(GetPlanUpgradeRequestsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        PlanUpgradeRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<PlanUpgradeRequestStatus>(query.Status, true, out var parsed))
            status = parsed;

        var all = await requestRepo.FindAsync(
            r => r.TenantId == tenantId && (!status.HasValue || r.Status == status),
            cancellationToken);

        var ordered = all.OrderByDescending(r => r.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var userIds = items.Select(r => r.UserId).Distinct().ToList();
        var users = await userRepo.FindAsync(u => userIds.Contains(u.Id), cancellationToken);
        var nameByUserId = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        var dtos = items.Select(r => new PlanUpgradeRequestDto
        {
            Id = r.Id,
            UserId = r.UserId,
            MemberName = nameByUserId.GetValueOrDefault(r.UserId, string.Empty),
            RequestedTier = r.RequestedTier.ToString(),
            PaymentReference = r.PaymentReference,
            Status = r.Status.ToString(),
            ReviewedAt = r.ReviewedAt,
            ReviewedByUserId = r.ReviewedByUserId,
            CreatedAt = r.CreatedAt,
        }).ToList();

        return new PagedResult<PlanUpgradeRequestDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
