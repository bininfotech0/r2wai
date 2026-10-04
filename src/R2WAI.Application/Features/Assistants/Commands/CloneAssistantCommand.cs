using FluentValidation;

namespace R2WAI.Application.Features.Assistants.Commands;

// docs/api/MISSING-BACKEND-ENDPOINTS.md §3.3 #65 — the brief's "Duplicate" card action.
public record CloneAssistantCommand : IRequest<AssistantDto>
{
    public Guid AssistantDefinitionId { get; init; }
}

public class CloneAssistantCommandValidator : AbstractValidator<CloneAssistantCommand>
{
    public CloneAssistantCommandValidator()
    {
        RuleFor(v => v.AssistantDefinitionId).NotEmpty();
    }
}

public class CloneAssistantCommandHandler(
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<CloneAssistantCommand, AssistantDto>
{
    public async Task<AssistantDto> Handle(CloneAssistantCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var source = await assistantRepo.GetByIdAsync(command.AssistantDefinitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantDefinitionId);

        var clone = new AssistantDefinition(
            Guid.NewGuid(), tenantId, $"{source.Name} (Copy)", source.Type,
            source.ModelConfigurationId, source.KnowledgeBaseId);

        // Verbatim copy, unlike CreateAssistantCommandHandler's DenyAllToolsByDefault() for a
        // brand-new assistant: a clone's whole point is to behave identically to its source until
        // edited, including a null Tools (legacy "all tools") if that's what the source has —
        // silently narrowing that on clone would be a surprising, undocumented behavior change.
        clone.UpdateDetails(source.Name + " (Copy)", source.Description, source.SystemPrompt,
            source.Tools, source.Settings, source.Tags, source.AvatarUrl);

        if (source.ApplicationId.HasValue)
            clone.AssignApplication(source.ApplicationId);

        // Deliberately NOT copied: PublishStatus/IsActive/PublishedVersion/UsageCount. A clone always
        // starts as a fresh, unpublished draft regardless of the source's own status — duplicating a
        // published assistant must never silently publish a second one.

        await assistantRepo.AddAsync(clone, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await AssistantCacheKeys.InvalidateAsync(cacheService, tenantId, cancellationToken);

        return mapper.Map<AssistantDto>(clone);
    }
}
