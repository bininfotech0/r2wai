using AutoMapper;
using R2WAI.Application.Features.Navigation.DTOs;

namespace R2WAI.Application.Features.Navigation.Mappings;

public class NavigationProfile : Profile
{
    public NavigationProfile()
    {
        CreateMap<NavigationDefinition, NavigationItemDto>();
    }
}
