using FluentValidation;
using R2WAI.Application.Features.Capabilities.DTOs;

namespace R2WAI.Application.Features.Capabilities.Commands;

public record CreateToolDefinitionVersionCommand : IRequest<ToolDefinitionVersionDto>
{
    public Guid ToolDefinitionId { get; init; }
    public string? Note { get; init; }
    public bool Publish { get; init; }
}

public class CreateToolDefinitionVersionCommandValidator : AbstractValidator<CreateToolDefinitionVersionCommand>
{
    public CreateToolDefinitionVersionCommandValidator()
    {
        RuleFor(v => v.ToolDefinitionId).NotEmpty();
        RuleFor(v => v.Note).MaximumLength(1000);
    }
}

public class CreateToolDefinitionVersionCommandHandler(
    IRepository<ToolDefinitionVersion> versionRepo,
    IRepository<ToolDefinition> toolRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateToolDefinitionVersionCommand, ToolDefinitionVersionDto>
{
    public async Task<ToolDefinitionVersionDto> Handle(CreateToolDefinitionVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var tool = await toolRepo.GetByIdAsync(command.ToolDefinitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.ToolDefinitionId);

        var snapshotJson = ToolDefinitionVersionSnapshotService.BuildSnapshotJson(tool);

        var existingVersions = await versionRepo.FindAsync(v => v.ToolDefinitionId == tool.Id, cancellationToken);
        var nextVersionNumber = existingVersions.Count == 0 ? 1 : existingVersions.Max(v => v.VersionNumber) + 1;

        var version = ToolDefinitionVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, tool.Id, nextVersionNumber, snapshotJson, command.Note);

        if (command.Publish)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedException();

            foreach (var published in existingVersions.Where(v => v.IsPublished))
                published.Unpublish();

            version.Publish(userId);
        }

        await versionRepo.AddAsync(version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ToolDefinitionVersionDto>(version);
    }
}
