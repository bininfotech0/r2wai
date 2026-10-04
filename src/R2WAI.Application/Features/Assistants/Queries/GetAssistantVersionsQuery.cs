using R2WAI.Application.Features.Assistants.DTOs;

namespace R2WAI.Application.Features.Assistants.Queries;

public record GetAssistantVersionsQuery : IRequest<List<AssistantVersionDto>>
{
    public Guid AssistantDefinitionId { get; init; }
}

public class GetAssistantVersionsQueryHandler(
    IRepository<AssistantVersion> versionRepo,
    IMapper mapper) : IRequestHandler<GetAssistantVersionsQuery, List<AssistantVersionDto>>
{
    public async Task<List<AssistantVersionDto>> Handle(GetAssistantVersionsQuery query, CancellationToken cancellationToken)
    {
        var versions = await versionRepo.FindAsync(v => v.AssistantDefinitionId == query.AssistantDefinitionId, cancellationToken);
        var ordered = versions.OrderByDescending(v => v.VersionNumber).ToList();

        return mapper.Map<List<AssistantVersionDto>>(ordered);
    }
}
