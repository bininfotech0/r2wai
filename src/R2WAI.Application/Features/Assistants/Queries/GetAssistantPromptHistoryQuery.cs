using R2WAI.Application.Features.Assistants.DTOs;

namespace R2WAI.Application.Features.Assistants.Queries;

public record GetAssistantPromptHistoryQuery : IRequest<List<AssistantPromptHistoryDto>>
{
    public Guid AssistantDefinitionId { get; init; }
}

public class GetAssistantPromptHistoryQueryHandler(
    IRepository<AssistantPromptHistory> historyRepo,
    IMapper mapper) : IRequestHandler<GetAssistantPromptHistoryQuery, List<AssistantPromptHistoryDto>>
{
    public async Task<List<AssistantPromptHistoryDto>> Handle(GetAssistantPromptHistoryQuery query, CancellationToken cancellationToken)
    {
        var history = await historyRepo.FindAsync(h => h.AssistantDefinitionId == query.AssistantDefinitionId, cancellationToken);
        var ordered = history.OrderByDescending(h => h.Version).ToList();

        return mapper.Map<List<AssistantPromptHistoryDto>>(ordered);
    }
}
