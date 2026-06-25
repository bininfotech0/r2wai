using AutoMapper;
using R2WAI.Application.Features.Documents.DTOs;

namespace R2WAI.Application.Features.Documents.Mappings;

public class DocumentProfile : Profile
{
    public DocumentProfile()
    {
        CreateMap<Document, DocumentDto>();
    }
}
