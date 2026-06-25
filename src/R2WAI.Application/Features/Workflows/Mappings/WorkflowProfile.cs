using AutoMapper;
using R2WAI.Application.Features.Workflows.DTOs;

namespace R2WAI.Application.Features.Workflows.Mappings;

public class WorkflowProfile : Profile
{
    public WorkflowProfile()
    {
        CreateMap<Workflow, WorkflowDto>();

        CreateMap<WorkflowInstance, WorkflowInstanceDto>()
            .ForMember(d => d.WorkflowName, o => o.MapFrom(s => s.Workflow != null ? s.Workflow.Name : null))
            .ForMember(d => d.Steps, o => o.Ignore()); // Steps require JSON deserialization — not a direct nav property
    }
}
