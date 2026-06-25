namespace R2WAI.Application.Features.KnowledgeBases.Queries;

public record GetKnowledgeBaseByIdQuery : IRequest<KnowledgeBaseDto>
{
    public Guid Id { get; init; }
}

public class GetKnowledgeBaseByIdQueryHandler(
    IRepository<KnowledgeBase> kbRepo,
    IMapper mapper) : IRequestHandler<GetKnowledgeBaseByIdQuery, KnowledgeBaseDto>
{
    public async Task<KnowledgeBaseDto> Handle(GetKnowledgeBaseByIdQuery query, CancellationToken cancellationToken)
    {
        var kb = await kbRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), query.Id);

        if (kb.IsDeleted)
            throw new NotFoundException(nameof(KnowledgeBase), query.Id);

        return mapper.Map<KnowledgeBaseDto>(kb);
    }
}
