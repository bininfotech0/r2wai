using R2WAI.Application.Features.KnowledgeBases.DTOs;

namespace R2WAI.Application.Features.KnowledgeBases.Queries;

public record GetKnowledgeBaseVersionsQuery : IRequest<List<KnowledgeBaseVersionDto>>
{
    public Guid KnowledgeBaseId { get; init; }
}

public class GetKnowledgeBaseVersionsQueryHandler(
    IRepository<KnowledgeBaseVersion> versionRepo,
    IMapper mapper) : IRequestHandler<GetKnowledgeBaseVersionsQuery, List<KnowledgeBaseVersionDto>>
{
    public async Task<List<KnowledgeBaseVersionDto>> Handle(GetKnowledgeBaseVersionsQuery query, CancellationToken cancellationToken)
    {
        var versions = await versionRepo.FindAsync(v => v.KnowledgeBaseId == query.KnowledgeBaseId, cancellationToken);
        var ordered = versions.OrderByDescending(v => v.VersionNumber).ToList();

        return mapper.Map<List<KnowledgeBaseVersionDto>>(ordered);
    }
}
