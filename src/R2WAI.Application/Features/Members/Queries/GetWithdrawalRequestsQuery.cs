namespace R2WAI.Application.Features.Members.Queries;

public record GetWithdrawalRequestsQuery : IRequest<PagedResult<WithdrawalRequestDto>>, IAuthorizedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class GetWithdrawalRequestsQueryHandler(
    IRepository<WithdrawalRequest> requestRepo,
    IRepository<User> userRepo,
    ICurrentUserService currentUser) : IRequestHandler<GetWithdrawalRequestsQuery, PagedResult<WithdrawalRequestDto>>
{
    public async Task<PagedResult<WithdrawalRequestDto>> Handle(GetWithdrawalRequestsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        WithdrawalRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<WithdrawalRequestStatus>(query.Status, true, out var parsed))
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

        var dtos = items.Select(r => new WithdrawalRequestDto
        {
            Id = r.Id,
            UserId = r.UserId,
            MemberName = nameByUserId.GetValueOrDefault(r.UserId, string.Empty),
            AmountRequested = r.AmountRequested,
            PayoutMethod = r.PayoutMethod,
            Status = r.Status.ToString(),
            ReviewedAt = r.ReviewedAt,
            ReviewedByUserId = r.ReviewedByUserId,
            CompletedAt = r.CompletedAt,
            AdminNotes = r.AdminNotes,
            CreatedAt = r.CreatedAt,
        }).ToList();

        return new PagedResult<WithdrawalRequestDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
