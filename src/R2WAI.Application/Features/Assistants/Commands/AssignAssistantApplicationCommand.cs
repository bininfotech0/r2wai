namespace R2WAI.Application.Features.Assistants.Commands;

public record AssignAssistantApplicationCommand : IRequest<AssistantDto>
{
    public Guid Id { get; init; }
    public Guid? ApplicationId { get; init; }
}

public class AssignAssistantApplicationCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<AssignAssistantApplicationCommand, AssistantDto>
{
    public async Task<AssistantDto> Handle(AssignAssistantApplicationCommand command, CancellationToken cancellationToken)
    {
        var assistant = await assistantRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.Id);

        assistant.AssignApplication(command.ApplicationId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<AssistantDto>(assistant);
    }
}
