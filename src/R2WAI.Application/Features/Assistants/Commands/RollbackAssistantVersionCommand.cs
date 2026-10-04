using FluentValidation;
using R2WAI.Application.Features.Assistants.DTOs;

namespace R2WAI.Application.Features.Assistants.Commands;

public record RollbackAssistantVersionCommand : IRequest<AssistantVersionDto>
{
    public Guid VersionId { get; init; }
}

public class RollbackAssistantVersionCommandValidator : AbstractValidator<RollbackAssistantVersionCommand>
{
    public RollbackAssistantVersionCommandValidator()
    {
        RuleFor(v => v.VersionId).NotEmpty();
    }
}

public class RollbackAssistantVersionCommandHandler(
    IRepository<AssistantVersion> versionRepo,
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RollbackAssistantVersionCommand, AssistantVersionDto>
{
    public async Task<AssistantVersionDto> Handle(RollbackAssistantVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var targetVersion = await versionRepo.GetByIdAsync(command.VersionId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantVersion), command.VersionId);

        var assistant = await assistantRepo.GetByIdAsync(targetVersion.AssistantDefinitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), targetVersion.AssistantDefinitionId);

        var snapshot = AssistantVersionSnapshotService.Deserialize(targetVersion.ConfigSnapshot);

        // Type is deliberately not restored — see AssistantVersionSnapshotService's doc comment,
        // there is no setter and the live value can never have diverged.
        assistant.UpdateDetails(snapshot.Name, snapshot.Description, snapshot.SystemPrompt,
            snapshot.Tools, snapshot.Settings, snapshot.Tags, snapshot.AvatarUrl);

        if (snapshot.ModelConfigurationId.HasValue)
            assistant.LinkModelConfiguration(snapshot.ModelConfigurationId.Value);

        if (snapshot.KnowledgeBaseId.HasValue)
            assistant.LinkKnowledgeBase(snapshot.KnowledgeBaseId.Value);

        var allVersions = await versionRepo.FindAsync(v => v.AssistantDefinitionId == assistant.Id, cancellationToken);
        var nextVersionNumber = allVersions.Max(v => v.VersionNumber) + 1;

        foreach (var published in allVersions.Where(v => v.IsPublished))
            published.Unpublish();

        // The restored version's snapshot is exactly the target's snapshot (that's what "restore" means) —
        // reusing it directly, same reasoning as RollbackKnowledgeBaseVersionCommandHandler.
        var newVersion = AssistantVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, assistant.Id, nextVersionNumber, targetVersion.ConfigSnapshot,
            $"Rolled back to v{targetVersion.VersionNumber}");
        newVersion.Publish(userId);

        await versionRepo.AddAsync(newVersion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<AssistantVersionDto>(newVersion);
    }
}
