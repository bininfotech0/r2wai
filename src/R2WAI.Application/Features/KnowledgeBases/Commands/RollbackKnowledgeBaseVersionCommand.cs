using FluentValidation;
using R2WAI.Application.Features.KnowledgeBases.DTOs;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record RollbackKnowledgeBaseVersionCommand : IRequest<KnowledgeBaseVersionDto>
{
    public Guid VersionId { get; init; }
}

public class RollbackKnowledgeBaseVersionCommandValidator : AbstractValidator<RollbackKnowledgeBaseVersionCommand>
{
    public RollbackKnowledgeBaseVersionCommandValidator()
    {
        RuleFor(v => v.VersionId).NotEmpty();
    }
}

public class RollbackKnowledgeBaseVersionCommandHandler(
    IRepository<KnowledgeBaseVersion> versionRepo,
    IRepository<KnowledgeBaseSource> sourceRepo,
    IRepository<KnowledgeBase> knowledgeBaseRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RollbackKnowledgeBaseVersionCommand, KnowledgeBaseVersionDto>
{
    public async Task<KnowledgeBaseVersionDto> Handle(RollbackKnowledgeBaseVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var targetVersion = await versionRepo.GetByIdAsync(command.VersionId, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBaseVersion), command.VersionId);

        var knowledgeBase = await knowledgeBaseRepo.GetByIdAsync(targetVersion.KnowledgeBaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), targetVersion.KnowledgeBaseId);

        var snapshot = KnowledgeBaseVersionSnapshotService.Deserialize(targetVersion.ConfigSnapshot);

        knowledgeBase.UpdateDetails(snapshot.Name, snapshot.Description);

        if (snapshot.EmbeddingModel is not null && snapshot.ChunkSize is not null
            && snapshot.ChunkOverlap is not null && snapshot.VectorCollectionName is not null)
        {
            knowledgeBase.ConfigureEmbedding(
                snapshot.EmbeddingModel, snapshot.ChunkSize.Value, snapshot.ChunkOverlap.Value, snapshot.VectorCollectionName);
        }

        var existingSources = await sourceRepo.FindAsync(s => s.KnowledgeBaseId == knowledgeBase.Id, cancellationToken);
        foreach (var existingSource in existingSources)
            sourceRepo.Delete(existingSource);

        foreach (var sourceSnapshot in snapshot.Sources)
        {
            var restoredSource = new KnowledgeBaseSource(
                Guid.NewGuid(), knowledgeBase.Id, sourceSnapshot.Type,
                sourceSnapshot.ReferenceId, sourceSnapshot.Url, sourceSnapshot.Content);

            await sourceRepo.AddAsync(restoredSource, cancellationToken);
        }

        var allVersions = await versionRepo.FindAsync(v => v.KnowledgeBaseId == knowledgeBase.Id, cancellationToken);
        var nextVersionNumber = allVersions.Max(v => v.VersionNumber) + 1;

        foreach (var published in allVersions.Where(v => v.IsPublished))
            published.Unpublish();

        // The restored version's snapshot is exactly the target's snapshot (that's what "restore" means) —
        // rebuilding it here would re-query the DB before SaveChanges and miss the just-added/removed sources.
        var newVersion = KnowledgeBaseVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, knowledgeBase.Id, nextVersionNumber, targetVersion.ConfigSnapshot,
            $"Rolled back to v{targetVersion.VersionNumber}");
        newVersion.Publish(userId);

        await versionRepo.AddAsync(newVersion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<KnowledgeBaseVersionDto>(newVersion);
    }
}
