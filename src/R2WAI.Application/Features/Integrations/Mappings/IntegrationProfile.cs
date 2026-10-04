using AutoMapper;
using R2WAI.Application.Common.Security;
using R2WAI.Application.Features.Integrations.DTOs;

namespace R2WAI.Application.Features.Integrations.Mappings;

public class IntegrationProfile : Profile
{
    public IntegrationProfile()
    {
        // Configuration's Token/ApiKey/Password never leave the server once saved — even encrypted,
        // there's no reason to hand the ciphertext to the client. AuthType/ApiKeyHeaderName/Username
        // (non-secret) still pass through so the edit form can show which auth type is configured.
        CreateMap<ToolDefinition, IntegrationDto>()
            .ForMember(d => d.Configuration, opt => opt.MapFrom(s => IntegrationCredentialCodec.Redact(s.Configuration)));
    }
}
