using AutoMapper;
using R2WAI.Application.Features.Leads.DTOs;

namespace R2WAI.Application.Features.Leads.Mappings;

public class LeadProfile : Profile
{
    public LeadProfile()
    {
        CreateMap<Lead, LeadDto>();
    }
}
