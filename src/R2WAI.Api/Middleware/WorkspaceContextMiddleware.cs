using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.Persistence;
using R2WAI.Infrastructure.Services;

namespace R2WAI.Api.Middleware;

/// <summary>
/// Resolves the X-Workspace-Id header into the request's <see cref="WorkspaceContext"/>. Runs after
/// TenantResolutionMiddleware, so the lookup goes through the tenant query filter: another tenant's
/// workspace id is simply not found. Not found, deleted or malformed falls back to the caller's
/// default workspace (narrower than no header, never wider). No header leaves the context empty,
/// which keeps tenant-wide behaviour for API clients and public routes.
/// </summary>
public class WorkspaceContextMiddleware(RequestDelegate next, ILogger<WorkspaceContextMiddleware> logger)
{
    public const string HeaderName = "X-Workspace-Id";

    public async Task InvokeAsync(HttpContext context, WorkspaceContext workspaceContext, ApplicationDbContext db)
    {
        var header = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header) && context.Items.ContainsKey("TenantId"))
        {
            var requested = Guid.TryParse(header, out var id)
                ? await db.Workspaces.Where(w => w.Id == id).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(context.RequestAborted)
                : null;

            if (requested is null)
                logger.LogInformation("Workspace header {Header} is not a live workspace of this tenant; using the default workspace", header);

            var resolved = requested
                ?? await db.Workspaces.Where(w => w.IsDefault).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(context.RequestAborted);

            if (resolved is { } workspaceId)
            {
                workspaceContext.Set(workspaceId);
                db.ActiveWorkspaceId = workspaceId;
            }
        }

        await next(context);
    }
}
