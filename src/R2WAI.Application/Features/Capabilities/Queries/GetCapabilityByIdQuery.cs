namespace R2WAI.Application.Features.Capabilities.Queries;

public record GetCapabilityByIdQuery : IRequest<CapabilityDto>
{
    public Guid Id { get; init; }
}

public class GetCapabilityByIdQueryHandler(
    IRepository<ToolDefinition> capabilityRepo,
    IMapper mapper) : IRequestHandler<GetCapabilityByIdQuery, CapabilityDto>
{
    public async Task<CapabilityDto> Handle(GetCapabilityByIdQuery query, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), query.Id);

        return mapper.Map<CapabilityDto>(capability);
    }
}
