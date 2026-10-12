using R2WAI.Application.Features.Workspaces.DTOs;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>Workspace reads that need grouped queries across several resource tables.</summary>
public interface IWorkspaceDirectory
{
    /// <summary>
    /// Live (not soft-deleted) agents, knowledge bases, automations, chatbots and connected systems
    /// per workspace of the current tenant. Workspaces with nothing in them are absent from the map.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, WorkspaceResourceCounts>> CountResourcesAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates the current tenant's default workspace if it has none, so the workspace list is never
    /// empty for a tenant that has not created anything yet.
    /// </summary>
    Task EnsureDefaultAsync(CancellationToken ct = default);
}
