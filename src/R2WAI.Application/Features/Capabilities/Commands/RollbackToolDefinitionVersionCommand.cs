using FluentValidation;
using R2WAI.Application.Features.Capabilities.DTOs;
using R2WAI.Domain.Enums;

namespace R2WAI.Application.Features.Capabilities.Commands;

public record RollbackToolDefinitionVersionCommand : IRequest<ToolDefinitionVersionDto>
{
    public Guid VersionId { get; init; }
}

public class RollbackToolDefinitionVersionCommandValidator : AbstractValidator<RollbackToolDefinitionVersionCommand>
{
    public RollbackToolDefinitionVersionCommandValidator()
    {
        RuleFor(v => v.VersionId).NotEmpty();
    }
}

public class RollbackToolDefinitionVersionCommandHandler(
    IRepository<ToolDefinitionVersion> versionRepo,
    IRepository<ToolDefinition> toolRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RollbackToolDefinitionVersionCommand, ToolDefinitionVersionDto>
{
    public async Task<ToolDefinitionVersionDto> Handle(RollbackToolDefinitionVersionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        var userId = currentUser.UserId ?? throw new UnauthorizedException();

        var targetVersion = await versionRepo.GetByIdAsync(command.VersionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinitionVersion), command.VersionId);

        var tool = await toolRepo.GetByIdAsync(targetVersion.ToolDefinitionId, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), targetVersion.ToolDefinitionId);

        var snapshot = ToolDefinitionVersionSnapshotService.Deserialize(targetVersion.ConfigSnapshot);

        var toolType = Enum.TryParse<ToolType>(snapshot.ToolType, out var parsedType) ? parsedType : ToolType.Http;

        tool.Update(snapshot.Name, snapshot.Description, toolType, snapshot.EndpointUrl, snapshot.Configuration);
        tool.LinkApi(snapshot.ApplicationApiId, snapshot.HttpMethod, snapshot.EndpointPath);
        tool.ConfigureGovernance(snapshot.RiskLevel, snapshot.RequiredRole,
            snapshot.ConfirmationRequired, snapshot.ApprovalRequired, snapshot.AuditRequired);

        var allVersions = await versionRepo.FindAsync(v => v.ToolDefinitionId == tool.Id, cancellationToken);
        var nextVersionNumber = allVersions.Max(v => v.VersionNumber) + 1;

        foreach (var published in allVersions.Where(v => v.IsPublished))
            published.Unpublish();

        // The restored version's snapshot is exactly the target's snapshot (that's what "restore" means) —
        // rebuilding it here would re-query the DB before SaveChanges and miss the just-updated entity.
        var newVersion = ToolDefinitionVersion.CreateSnapshot(
            Guid.NewGuid(), tenantId, tool.Id, nextVersionNumber, targetVersion.ConfigSnapshot,
            $"Rolled back to v{targetVersion.VersionNumber}");
        newVersion.Publish(userId);

        await versionRepo.AddAsync(newVersion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ToolDefinitionVersionDto>(newVersion);
    }
}
