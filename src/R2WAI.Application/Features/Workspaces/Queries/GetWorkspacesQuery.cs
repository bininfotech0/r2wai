using R2WAI.Application.Features.Workspaces.DTOs;

namespace R2WAI.Application.Features.Workspaces.Queries;

/// <summary>Every workspace of the current tenant, default first, each with its resource counts.</summary>
public record GetWorkspacesQuery : IRequest<List<WorkspaceDto>>;

public class GetWorkspacesQueryHandler(
    IRepository<Workspace> workspaceRepo,
    IWorkspaceDirectory directory) : IRequestHandler<GetWorkspacesQuery, List<WorkspaceDto>>
{
    public async Task<List<WorkspaceDto>> Handle(GetWorkspacesQuery query, CancellationToken cancellationToken)
    {
        await directory.EnsureDefaultAsync(cancellationToken);

        // Tenant and soft-delete filters are global query filters.
        var workspaces = await workspaceRepo.FindAsync(_ => true, cancellationToken);
        var counts = await directory.CountResourcesAsync(cancellationToken);

        return workspaces
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .Select(w => WorkspaceDto.From(w, counts.GetValueOrDefault(w.Id)))
            .ToList();
    }
}

public record GetWorkspaceByIdQuery(Guid Id) : IRequest<WorkspaceDto>;

public class GetWorkspaceByIdQueryHandler(
    IRepository<Workspace> workspaceRepo,
    IWorkspaceDirectory directory) : IRequestHandler<GetWorkspaceByIdQuery, WorkspaceDto>
{
    public async Task<WorkspaceDto> Handle(GetWorkspaceByIdQuery query, CancellationToken cancellationToken)
    {
        var workspace = await workspaceRepo.FirstOrDefaultAsync(w => w.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Workspace), query.Id);
        var counts = await directory.CountResourcesAsync(cancellationToken);
        return WorkspaceDto.From(workspace, counts.GetValueOrDefault(workspace.Id));
    }
}
