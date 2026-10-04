using FluentValidation;
using R2WAI.Application.Features.KnowledgeBases.DTOs;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record CreateKnowledgeBaseVersionCommand : IRequest<KnowledgeBaseVersionDto>
{
    public Guid KnowledgeBaseId { get; init; }
    public string? Note { get; init; }
    public bool Publish { get; init; }
}

public class CreateKnowledgeBaseVersionCommandValidator : AbstractValidator<CreateKnowledgeBaseVersionCommand>
{
    public CreateKnowledgeBaseVersionCommandValidator()
    {
        RuleFor(v => v.KnowledgeBaseId).NotEmpty();
        RuleFor(v => v.Note).MaximumLength(1000);
    }
}

public class CreateKnowledgeBaseVersionCommandHandler(
    IRepository<KnowledgeBaseVersion> versionRepo,
    IRepository<KnowledgeBaseSource> sourceRepo,
    IRepository<KnowledgeBase> knowledgeBaseRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateKnowledgeBaseVersionCommand, KnowledgeBaseVersionDto>
{
    public async Task<KnowledgeBaseVersionDto> Handle(CreateKnowledgeBaseVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var knowledgeBase = await knowledgeBaseRepo.GetByIdAsync(command.KnowledgeBaseId, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), command.KnowledgeBaseId);

        var snapshotJson = await KnowledgeBaseVersionSnapshotService.BuildSnapshotJsonAsync(
            knowledgeBase, sourceRepo, cancellationToken);

        var existingVersions = await versionRepo.FindAsync(v => v.KnowledgeBaseId == knowledgeBase.Id, cancellationToken);
        var nextVersionNumber = existingVersions.Count == 0 ? 1 : existingVersions.Max(v => v.VersionNumber) + 1;

        var version = KnowledgeBaseVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, knowledgeBase.Id, nextVersionNumber, snapshotJson, command.Note);

        if (command.Publish)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedException();

            foreach (var published in existingVersions.Where(v => v.IsPublished))
                published.Unpublish();

            version.Publish(userId);
        }

        await versionRepo.AddAsync(version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<KnowledgeBaseVersionDto>(version);
    }
}
