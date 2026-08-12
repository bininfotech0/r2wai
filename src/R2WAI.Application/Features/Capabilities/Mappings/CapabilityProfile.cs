using AutoMapper;
using R2WAI.Application.Features.Capabilities.DTOs;

namespace R2WAI.Application.Features.Capabilities.Mappings;

public class CapabilityProfile : Profile
{
    public CapabilityProfile()
    {
        CreateMap<ToolDefinition, CapabilityDto>();
    }
}
