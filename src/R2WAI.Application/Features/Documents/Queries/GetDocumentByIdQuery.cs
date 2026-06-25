namespace R2WAI.Application.Features.Documents.Queries;

public record GetDocumentByIdQuery : IRequest<DocumentDto>
{
    public Guid Id { get; init; }
}

public class GetDocumentByIdQueryHandler(
    IRepository<Document> documentRepo,
    IMapper mapper) : IRequestHandler<GetDocumentByIdQuery, DocumentDto>
{
    public async Task<DocumentDto> Handle(GetDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), query.Id);

        if (document.IsDeleted)
            throw new NotFoundException(nameof(Document), query.Id);

        return mapper.Map<DocumentDto>(document);
    }
}
