namespace R2WAI.Domain.Common;

/// <summary>
/// A tenant-owned resource that also belongs to one workspace. WorkspaceId is nullable only so the
/// column could be added to existing tables; ApplicationDbContext assigns the tenant's default
/// workspace to any such row saved without one, so after the backfill migration every row has one.
/// </summary>
public interface IWorkspaceScoped
{
    Guid TenantId { get; }
    Guid? WorkspaceId { get; }
    void MoveToWorkspace(Guid workspaceId);
}
