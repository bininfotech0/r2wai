using AutoMapper;
using R2WAI.Application.Features.Assistants.DTOs;

namespace R2WAI.Application.Features.Assistants.Mappings;

public class AssistantProfile : Profile
{
    public AssistantProfile()
    {
        CreateMap<AssistantDefinition, AssistantDto>()
            .ForMember(d => d.PublishStatus, opt => opt.MapFrom(s => s.PublishStatus.ToString()));
    }
}
