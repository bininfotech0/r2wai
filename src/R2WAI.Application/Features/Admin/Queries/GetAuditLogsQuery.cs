using System.Linq.Expressions;

namespace R2WAI.Application.Features.Admin.Queries;

public record GetAuditLogsQuery : IRequest<PagedResult<AuditLogDto>>, IAuthorizedRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public Guid? UserId { get; init; }
    public Guid? ApplicationId { get; init; }
    public AuditAction? Action { get; init; }
    public string? EntityType { get; init; }
    // Per-entity views (e.g. a Tool's Usage tab, a Run's audit trail) need to
    // narrow to one row's history — EntityType alone can't do that, and every
    // caller of this query was filtering the full EntityType page client-side.
    public string? EntityId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class GetAuditLogsQueryHandler(
    IRepository<AuditLog> auditLogRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filter =
            (Expression<Func<AuditLog, bool>>)(l => l.TenantId == tenantId
              && (!query.UserId.HasValue || l.UserId == query.UserId)
              && (!query.ApplicationId.HasValue || l.ApplicationId == query.ApplicationId)
              && (!query.Action.HasValue || l.Action == query.Action)
              && (string.IsNullOrWhiteSpace(query.EntityType) || l.EntityType == query.EntityType)
              && (string.IsNullOrWhiteSpace(query.EntityId) || l.EntityId == query.EntityId)
              && (!query.From.HasValue || l.Timestamp >= query.From.Value)
              && (!query.To.HasValue || l.Timestamp <= query.To.Value));

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 1000);
        var result = await auditLogRepo.FindPagedAsync(
            filter,
            l => l.Timestamp,
            page,
            pageSize,
            descending: true,
            cancellationToken: cancellationToken);

        return new PagedResult<AuditLogDto>
        {
            Items = mapper.Map<List<AuditLogDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
