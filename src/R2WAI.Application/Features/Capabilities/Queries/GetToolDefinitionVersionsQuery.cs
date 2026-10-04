using R2WAI.Application.Features.Capabilities.DTOs;

namespace R2WAI.Application.Features.Capabilities.Queries;

public record GetToolDefinitionVersionsQuery : IRequest<List<ToolDefinitionVersionDto>>
{
    public Guid ToolDefinitionId { get; init; }
}

public class GetToolDefinitionVersionsQueryHandler(
    IRepository<ToolDefinitionVersion> versionRepo,
    IMapper mapper) : IRequestHandler<GetToolDefinitionVersionsQuery, List<ToolDefinitionVersionDto>>
{
    public async Task<List<ToolDefinitionVersionDto>> Handle(GetToolDefinitionVersionsQuery query, CancellationToken cancellationToken)
    {
        var versions = await versionRepo.FindAsync(v => v.ToolDefinitionId == query.ToolDefinitionId, cancellationToken);
        var ordered = versions.OrderByDescending(v => v.VersionNumber).ToList();

        return mapper.Map<List<ToolDefinitionVersionDto>>(ordered);
    }
}
