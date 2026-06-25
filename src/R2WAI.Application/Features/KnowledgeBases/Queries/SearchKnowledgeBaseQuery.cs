namespace R2WAI.Application.Features.KnowledgeBases.Queries;

public record SearchKnowledgeBaseQuery : IRequest<PagedResult<SearchResultDto>>
{
    public Guid KnowledgeBaseId { get; init; }
    public string Query { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class SearchKnowledgeBaseQueryHandler(
    IKnowledgeBaseService knowledgeBaseService) : IRequestHandler<SearchKnowledgeBaseQuery, PagedResult<SearchResultDto>>
{
    public async Task<PagedResult<SearchResultDto>> Handle(SearchKnowledgeBaseQuery query, CancellationToken cancellationToken)
    {
        return await knowledgeBaseService.SearchKnowledgeBaseAsync(
            query.KnowledgeBaseId, query.Query, query.Page, query.PageSize, cancellationToken);
    }
}
