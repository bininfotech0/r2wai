using AutoMapper;
using R2WAI.Application.Features.Assistants.DTOs;

namespace R2WAI.Application.Features.Assistants.Mappings;

public class AssistantProfile : Profile
{
    public AssistantProfile()
    {
        CreateMap<AssistantDefinition, AssistantDto>();
    }
}
