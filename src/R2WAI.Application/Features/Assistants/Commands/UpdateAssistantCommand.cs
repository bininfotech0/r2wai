using FluentValidation;

namespace R2WAI.Application.Features.Assistants.Commands;

public record UpdateAssistantCommand : IRequest<AssistantDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public AssistantType? Type { get; init; }
    public string? SystemPrompt { get; init; }
    public Guid? ModelConfigurationId { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public string? Tools { get; init; }
    public string? Settings { get; init; }
    public bool? IsActive { get; init; }
}

public class UpdateAssistantCommandValidator : AbstractValidator<UpdateAssistantCommand>
{
    public UpdateAssistantCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateAssistantCommand, AssistantDto>
{
    public async Task<AssistantDto> Handle(UpdateAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = await assistantRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.Id);

        assistant.UpdateDetails(command.Name, command.Description,
            command.SystemPrompt, command.Tools, command.Settings);

        if (command.ModelConfigurationId.HasValue)
            assistant.LinkModelConfiguration(command.ModelConfigurationId.Value);

        if (command.KnowledgeBaseId.HasValue)
            assistant.LinkKnowledgeBase(command.KnowledgeBaseId.Value);

        if (command.IsActive == true)
            assistant.Publish();
        else if (command.IsActive == false)
            assistant.Unpublish();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<AssistantDto>(assistant);
    }
}
