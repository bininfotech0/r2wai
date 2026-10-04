using AutoMapper;
using R2WAI.Application.Features.KnowledgeBases.DTOs;

namespace R2WAI.Application.Features.KnowledgeBases.Mappings;

public class KnowledgeBaseProfile : Profile
{
    public KnowledgeBaseProfile()
    {
        CreateMap<KnowledgeBase, KnowledgeBaseDto>();

        CreateMap<KnowledgeBaseSource, KnowledgeBaseSourceDto>()
            .ForMember(d => d.KnowledgeBaseName, opt => opt.MapFrom(s => s.KnowledgeBase != null ? s.KnowledgeBase.Name : null));

        CreateMap<KnowledgeBaseVersion, KnowledgeBaseVersionDto>();
    }
}
