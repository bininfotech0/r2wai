using R2WAI.Application.Features.BusinessCapabilities.DTOs;

namespace R2WAI.Application.Features.BusinessCapabilities.Queries;

public record GetBusinessCapabilityByIdQuery : IRequest<BusinessCapabilityDto>
{
    public Guid Id { get; init; }
}

public class GetBusinessCapabilityByIdQueryHandler(
    IRepository<BusinessCapability> capabilityRepo,
    IMapper mapper) : IRequestHandler<GetBusinessCapabilityByIdQuery, BusinessCapabilityDto>
{
    public async Task<BusinessCapabilityDto> Handle(GetBusinessCapabilityByIdQuery query, CancellationToken cancellationToken)
    {
        var capability = await capabilityRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BusinessCapability), query.Id);

        return mapper.Map<BusinessCapabilityDto>(capability);
    }
}
