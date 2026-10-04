using FluentValidation;
using R2WAI.Application.Features.Assistants.DTOs;

namespace R2WAI.Application.Features.Assistants.Commands;

public record CreateAssistantVersionCommand : IRequest<AssistantVersionDto>
{
    public Guid AssistantDefinitionId { get; init; }
    public string? Note { get; init; }
    public bool Publish { get; init; }
}

public class CreateAssistantVersionCommandValidator : AbstractValidator<CreateAssistantVersionCommand>
{
    public CreateAssistantVersionCommandValidator()
    {
        RuleFor(v => v.AssistantDefinitionId).NotEmpty();
        RuleFor(v => v.Note).MaximumLength(1000);
    }
}

public class CreateAssistantVersionCommandHandler(
    IRepository<AssistantVersion> versionRepo,
    IRepository<AssistantDefinition> assistantRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateAssistantVersionCommand, AssistantVersionDto>
{
    public async Task<AssistantVersionDto> Handle(CreateAssistantVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var assistant = await assistantRepo.GetByIdAsync(command.AssistantDefinitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(AssistantDefinition), command.AssistantDefinitionId);

        var snapshotJson = AssistantVersionSnapshotService.BuildSnapshotJson(assistant);

        var existingVersions = await versionRepo.FindAsync(v => v.AssistantDefinitionId == assistant.Id, cancellationToken);
        var nextVersionNumber = existingVersions.Count == 0 ? 1 : existingVersions.Max(v => v.VersionNumber) + 1;

        var version = AssistantVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, assistant.Id, nextVersionNumber, snapshotJson, command.Note);

        if (command.Publish)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedException();

            foreach (var published in existingVersions.Where(v => v.IsPublished))
                published.Unpublish();

            version.Publish(userId);
        }

        await versionRepo.AddAsync(version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<AssistantVersionDto>(version);
    }
}
