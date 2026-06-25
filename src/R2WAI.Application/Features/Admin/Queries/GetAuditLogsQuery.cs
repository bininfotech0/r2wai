namespace R2WAI.Application.Features.Admin.Queries;

public record GetAuditLogsQuery : IRequest<PagedResult<AuditLogDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public Guid? UserId { get; init; }
    public AuditAction? Action { get; init; }
    public string? EntityType { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public class GetAuditLogsQueryHandler(
    IRepository<AuditLog> auditLogRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var all = await auditLogRepo.FindAsync(
            l => l.TenantId == tenantId
              && (!query.UserId.HasValue || l.UserId == query.UserId)
              && (!query.Action.HasValue || l.Action == query.Action)
              && (string.IsNullOrWhiteSpace(query.EntityType) || l.EntityType == query.EntityType)
              && (!query.From.HasValue || l.Timestamp >= query.From.Value)
              && (!query.To.HasValue || l.Timestamp <= query.To.Value),
            cancellationToken);

        var ordered = all.OrderByDescending(l => l.Timestamp);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<AuditLogDto>
        {
            Items = mapper.Map<List<AuditLogDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
