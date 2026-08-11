using AutoMapper;
using R2WAI.Application.Features.Applications.DTOs;

namespace R2WAI.Application.Features.Applications.Mappings;

public class ApplicationProfile : Profile
{
    public ApplicationProfile()
    {
        CreateMap<ConnectedApplication, ApplicationDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Environment, opt => opt.MapFrom(s => s.Environment.ToString()));

        CreateMap<Department, DepartmentDto>();

        CreateMap<ApplicationApi, ApplicationApiDto>()
            .ForMember(d => d.AuthScheme, opt => opt.MapFrom(s => s.AuthScheme.ToString()));

        CreateMap<ApplicationConfiguration, ApplicationConfigurationDto>();

        CreateMap<ApplicationVersion, ApplicationVersionDto>();
    }
}
