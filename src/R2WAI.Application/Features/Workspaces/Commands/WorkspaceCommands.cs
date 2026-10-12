using FluentValidation;
using R2WAI.Application.Features.Workspaces.DTOs;

namespace R2WAI.Application.Features.Workspaces.Commands;

public record CreateWorkspaceCommand : IRequest<WorkspaceDto>
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record UpdateWorkspaceCommand : IRequest<WorkspaceDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record DeleteWorkspaceCommand(Guid Id) : IRequest<Unit>;

public class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
    }
}

public class UpdateWorkspaceCommandValidator : AbstractValidator<UpdateWorkspaceCommand>
{
    public UpdateWorkspaceCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
    }
}

public class CreateWorkspaceCommandHandler(
    IRepository<Workspace> workspaceRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IRequestHandler<CreateWorkspaceCommand, WorkspaceDto>
{
    public async Task<WorkspaceDto> Handle(CreateWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();
        await WorkspaceRules.EnsureNameIsFreeAsync(workspaceRepo, command.Name, null, cancellationToken);

        var workspace = new Workspace(Guid.NewGuid(), tenantId, command.Name, command.Description, currentUser.UserId);
        await workspaceRepo.AddAsync(workspace, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return WorkspaceDto.From(workspace);
    }
}

public class UpdateWorkspaceCommandHandler(
    IRepository<Workspace> workspaceRepo,
    IUnitOfWork unitOfWork,
    IWorkspaceDirectory directory) : IRequestHandler<UpdateWorkspaceCommand, WorkspaceDto>
{
    public async Task<WorkspaceDto> Handle(UpdateWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var workspace = await workspaceRepo.FirstOrDefaultAsync(w => w.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workspace), command.Id);
        await WorkspaceRules.EnsureNameIsFreeAsync(workspaceRepo, command.Name, workspace.Id, cancellationToken);

        workspace.UpdateDetails(command.Name, command.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var counts = await directory.CountResourcesAsync(cancellationToken);
        return WorkspaceDto.From(workspace, counts.GetValueOrDefault(workspace.Id));
    }
}

/// <summary>
/// Soft-deletes an empty, non-default workspace. Refusing a non-empty one is the point: the
/// foreign keys are Restrict, but a soft delete never reaches them, so without this check the
/// resources inside would be left pointing at a workspace nobody can see.
/// </summary>
public class DeleteWorkspaceCommandHandler(
    IRepository<Workspace> workspaceRepo,
    IUnitOfWork unitOfWork,
    IWorkspaceDirectory directory) : IRequestHandler<DeleteWorkspaceCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWorkspaceCommand command, CancellationToken cancellationToken)
    {
        var workspace = await workspaceRepo.FirstOrDefaultAsync(w => w.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workspace), command.Id);

        if (workspace.IsDefault)
            throw new ValidationException("Id", "The default workspace cannot be deleted.");

        var counts = await directory.CountResourcesAsync(cancellationToken);
        if (counts.GetValueOrDefault(workspace.Id) is { Total: > 0 } inUse)
            throw new ValidationException("Id",
                $"'{workspace.Name}' still contains {inUse.Total} item(s). Move or delete them first.");

        workspaceRepo.Delete(workspace);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

internal static class WorkspaceRules
{
    /// <summary>Case-insensitive, trimmed, within the current tenant (global query filter).</summary>
    public static async Task EnsureNameIsFreeAsync(
        IRepository<Workspace> workspaceRepo, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();
        var clash = await workspaceRepo.FirstOrDefaultAsync(
            w => w.Name.ToLower() == normalized && (excludeId == null || w.Id != excludeId),
            cancellationToken);

        if (clash is not null)
            throw new ValidationException("Name", $"A workspace named '{clash.Name}' already exists.");
    }
}
