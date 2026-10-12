namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// The workspace the current request works in, chosen with the Studio's workspace switcher and sent
/// as the X-Workspace-Id header. Only ever set to a live workspace of the caller's own tenant
/// (WorkspaceContextMiddleware checks it; an unknown id falls back to the tenant's default), so it
/// can narrow what a request sees but never widen it.
///
/// Null when the request carries no header: API-key clients, public chat routes and background jobs
/// keep tenant-wide behaviour. List queries filter by it; lookups by id do not, so a link to an item
/// in another workspace of the same tenant still opens.
/// </summary>
public interface IWorkspaceContext
{
    Guid? WorkspaceId { get; }
}
