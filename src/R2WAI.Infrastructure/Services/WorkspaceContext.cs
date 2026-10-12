using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

/// <summary>Per-request holder for <see cref="IWorkspaceContext"/>; set once by WorkspaceContextMiddleware.</summary>
public sealed class WorkspaceContext : IWorkspaceContext
{
    public Guid? WorkspaceId { get; private set; }

    public void Set(Guid workspaceId) => WorkspaceId = workspaceId;
}
