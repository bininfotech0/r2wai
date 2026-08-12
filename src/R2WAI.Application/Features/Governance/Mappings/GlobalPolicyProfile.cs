using AutoMapper;
using R2WAI.Application.Features.Governance.DTOs;

namespace R2WAI.Application.Features.Governance.Mappings;

public class GlobalPolicyProfile : Profile
{
    public GlobalPolicyProfile()
    {
        CreateMap<GlobalPolicy, GlobalPolicyDto>();
    }
}
