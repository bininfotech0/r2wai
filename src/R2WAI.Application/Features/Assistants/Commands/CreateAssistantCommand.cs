using FluentValidation;

namespace R2WAI.Application.Features.Assistants.Commands;

public record CreateAssistantCommand : IRequest<AssistantDto>
{
    public string Name { get; init; } = string.Empty;
    public AssistantType Type { get; init; }
    public Guid? ModelConfigurationId { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
}

public class CreateAssistantCommandValidator : AbstractValidator<CreateAssistantCommand>
{
    public CreateAssistantCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateAssistantCommand, AssistantDto>
{
    public async Task<AssistantDto> Handle(CreateAssistantCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var assistant = new AssistantDefinition(
            Guid.NewGuid(), tenantId, command.Name, command.Type,
            command.ModelConfigurationId, command.KnowledgeBaseId);

        await assistantRepo.AddAsync(assistant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<AssistantDto>(assistant);
    }
}
