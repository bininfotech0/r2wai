using R2WAI.Application.Features.BusinessCapabilities.DTOs;

namespace R2WAI.Application.Features.BusinessCapabilities.Queries;

public record GetBusinessCapabilitiesQuery : IRequest<PagedResult<BusinessCapabilityDto>>
{
    public Guid AssistantId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetBusinessCapabilitiesQueryHandler(
    IRepository<BusinessCapability> capabilityRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetBusinessCapabilitiesQuery, PagedResult<BusinessCapabilityDto>>
{
    public async Task<PagedResult<BusinessCapabilityDto>> Handle(GetBusinessCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var filtered = await capabilityRepo.FindAsync(
            c => c.TenantId == tenantId && c.AssistantId == query.AssistantId && !c.IsDeleted,
            cancellationToken);

        var ordered = filtered.OrderByDescending(c => c.CreatedAt);
        var total = ordered.Count();
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return new PagedResult<BusinessCapabilityDto>
        {
            Items = mapper.Map<List<BusinessCapabilityDto>>(items),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }
}
