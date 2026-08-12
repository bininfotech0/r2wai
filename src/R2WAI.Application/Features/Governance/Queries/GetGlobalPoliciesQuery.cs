namespace R2WAI.Application.Features.Governance.Queries;

public record GetGlobalPoliciesQuery : IRequest<List<GlobalPolicyDto>>;

public class GetGlobalPoliciesQueryHandler(
    IRepository<GlobalPolicy> policyRepo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetGlobalPoliciesQuery, List<GlobalPolicyDto>>
{
    public async Task<List<GlobalPolicyDto>> Handle(GetGlobalPoliciesQuery query, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var policies = await policyRepo.FindAsync(p => p.TenantId == tenantId, cancellationToken);
        return mapper.Map<List<GlobalPolicyDto>>(policies.OrderBy(p => p.Type).ToList());
    }
}
