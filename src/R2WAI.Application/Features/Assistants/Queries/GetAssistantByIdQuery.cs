namespace R2WAI.Application.Features.Assistants.Queries;

public record GetAssistantByIdQuery : IRequest<AssistantDto>
{
    public Guid Id { get; init; }
}

public class GetAssistantByIdQueryHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IMapper mapper) : IRequestHandler<GetAssistantByIdQuery, AssistantDto>
{
    public async Task<AssistantDto> Handle(GetAssistantByIdQuery query, CancellationToken cancellationToken)
    {
        var assistant = await assistantRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), query.Id);

        if (assistant.IsDeleted)
            throw new NotFoundException(nameof(AssistantDefinition), query.Id);

        return mapper.Map<AssistantDto>(assistant);
    }
}
