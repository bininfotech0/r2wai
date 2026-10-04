using AutoMapper;
using R2WAI.Application.Features.Tenants.DTOs;

namespace R2WAI.Application.Features.Tenants.Mappings;

public class TenantProfile : Profile
{
    public TenantProfile()
    {
        CreateMap<Tenant, TenantDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));
    }
}
