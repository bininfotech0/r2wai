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
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<CreateAssistantCommand, AssistantDto>
{
    public async Task<AssistantDto> Handle(CreateAssistantCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var assistant = new AssistantDefinition(
            Guid.NewGuid(), tenantId, command.Name, command.Type,
            command.ModelConfigurationId, command.KnowledgeBaseId);
        // P0-4 (2026-09-20 audit): an assistant whose Tools tab was never touched used to silently get
        // every tool in the tenant. Existing assistants keep that behavior (GetEnabledToolIds' null
        // fallback is unchanged), but a brand-new one now starts deny-by-default — an admin opts it
        // into tools explicitly, via the same Tools tab UpdateDetails already uses.
        assistant.DenyAllToolsByDefault();

        await assistantRepo.AddAsync(assistant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await AssistantCacheKeys.InvalidateAsync(cacheService, tenantId, cancellationToken);

        return mapper.Map<AssistantDto>(assistant);
    }
}
