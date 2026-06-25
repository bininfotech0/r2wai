using AutoMapper;
using R2WAI.Application.Features.Integrations.DTOs;

namespace R2WAI.Application.Features.Integrations.Mappings;

public class IntegrationProfile : Profile
{
    public IntegrationProfile()
    {
        CreateMap<ToolDefinition, IntegrationDto>();
    }
}
