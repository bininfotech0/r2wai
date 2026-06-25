using AutoMapper;
using R2WAI.Application.Features.Admin.DTOs;

namespace R2WAI.Application.Features.Admin.Mappings;

public class AdminProfile : Profile
{
    public AdminProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.Roles, o => o.MapFrom(s => s.UserRoles.Select(ur => ur.Role.Name).ToList()));

        CreateMap<Role, RoleDto>();

        CreateMap<AuditLog, AuditLogDto>()
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? $"{s.User.FirstName} {s.User.LastName}" : null));

        CreateMap<ModelConfiguration, ModelConfigDto>()
            .ForMember(d => d.HasApiKey, o => o.MapFrom(s => !string.IsNullOrEmpty(s.ApiKeyEncrypted)));
    }
}
