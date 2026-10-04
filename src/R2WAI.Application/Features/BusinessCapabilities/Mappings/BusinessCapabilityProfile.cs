using AutoMapper;
using R2WAI.Application.Features.BusinessCapabilities.DTOs;

namespace R2WAI.Application.Features.BusinessCapabilities.Mappings;

public class BusinessCapabilityProfile : Profile
{
    public BusinessCapabilityProfile()
    {
        CreateMap<BusinessCapability, BusinessCapabilityDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ToolIds, o => o.MapFrom(s => s.GetToolIds()))
            .ForMember(d => d.KnowledgeBaseIds, o => o.MapFrom(s => s.GetKnowledgeBaseIds()))
            .ForMember(d => d.WorkflowIds, o => o.MapFrom(s => s.GetWorkflowIds()))
            .ForMember(d => d.ApplicationApiIds, o => o.MapFrom(s => s.GetApplicationApiIds()));
    }
}
