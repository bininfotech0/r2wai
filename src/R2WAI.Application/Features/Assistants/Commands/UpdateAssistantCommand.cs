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
    // Guid? can't distinguish "field omitted" (preserve current value) from "explicit null"
    // (clear it) once JSON-bound — several call sites (e.g. CreateAssistantDialog) send a partial
    // payload that omits these fields and must not have them cleared as a side effect. These
    // flags make "reset to tenant default" / "detach the knowledge base" explicit, unambiguous
    // requests instead of overloading null. Real, live bug this closes: AssistantStudioPage's
    // "Base Model" selector already offers "Use tenant default" (value ""), which serialized to
    // `modelConfigurationId: undefined` — omitted from the request body, so the existing
    // ModelConfigurationId was silently never cleared; the control looked like it worked but didn't.
    public bool UnlinkModelConfiguration { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
    public bool UnlinkKnowledgeBase { get; init; }
    public string? Tools { get; init; }
    public string? Settings { get; init; }
    public bool? IsActive { get; init; }
    public string? Tags { get; init; }
    public string? AvatarUrl { get; init; }
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
    IRepository<AssistantPromptHistory> promptHistoryRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ICacheService cacheService,
    IMapper mapper) : IRequestHandler<UpdateAssistantCommand, AssistantDto>
{
    public async Task<AssistantDto> Handle(UpdateAssistantCommand command, CancellationToken cancellationToken)
    {
        var assistant = await assistantRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.Id);

        // Record prompt history only on a real change — editing Name/Tools/etc. with the same
        // SystemPrompt (or the command simply not touching it) must not create a no-op history row.
        // Mirrors PromptTemplateService.SetTemplateAsync's supersede-then-add shape, per-instance.
        if (command.SystemPrompt is not null && command.SystemPrompt != assistant.SystemPrompt
            && currentUser.TenantId is { } promptTenantId)
        {
            var existingHistory = await promptHistoryRepo.FindAsync(
                h => h.AssistantDefinitionId == assistant.Id, cancellationToken);
            var currentEntry = existingHistory.FirstOrDefault(h => h.IsActive);
            currentEntry?.Supersede();

            var nextVersion = existingHistory.Count == 0 ? 1 : existingHistory.Max(h => h.Version) + 1;
            var historyEntry = new AssistantPromptHistory(
                Guid.NewGuid(), promptTenantId, assistant.Id, command.SystemPrompt, nextVersion);
            await promptHistoryRepo.AddAsync(historyEntry, cancellationToken);
        }

        assistant.UpdateDetails(command.Name, command.Description,
            command.SystemPrompt, command.Tools, command.Settings,
            command.Tags, command.AvatarUrl);

        if (command.UnlinkModelConfiguration)
            assistant.UnlinkModelConfiguration();
        else if (command.ModelConfigurationId.HasValue)
            assistant.LinkModelConfiguration(command.ModelConfigurationId.Value);

        if (command.UnlinkKnowledgeBase)
            assistant.UnlinkKnowledgeBase();
        else if (command.KnowledgeBaseId.HasValue)
            assistant.LinkKnowledgeBase(command.KnowledgeBaseId.Value);

        if (command.IsActive == true)
            assistant.Publish();
        else if (command.IsActive == false)
            assistant.Unpublish();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tenantId = currentUser.TenantId;
        if (tenantId.HasValue)
        {
            await AssistantCacheKeys.InvalidateAsync(cacheService, tenantId.Value, cancellationToken);
        }

        return mapper.Map<AssistantDto>(assistant);
    }
}
