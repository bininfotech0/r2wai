namespace R2WAI.Application.Features.Integrations.Queries;

public class GetIntegrationByIdQueryHandler(
    IRepository<ToolDefinition> repo,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetIntegrationByIdQuery, IntegrationDto>
{
    public async Task<IntegrationDto> Handle(GetIntegrationByIdQuery query, CancellationToken cancellationToken)
    {
        var tool = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), query.Id);

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        if (tool.TenantId != tenantId)
            throw new UnauthorizedException();

        return mapper.Map<IntegrationDto>(tool);
    }
}
