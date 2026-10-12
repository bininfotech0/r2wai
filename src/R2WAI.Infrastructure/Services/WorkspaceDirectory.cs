using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Features.Workspaces.DTOs;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.Services;

public sealed class WorkspaceDirectory(ApplicationDbContext context, ICurrentUserService currentUser) : IWorkspaceDirectory
{
    public async Task<IReadOnlyDictionary<Guid, WorkspaceResourceCounts>> CountResourcesAsync(CancellationToken ct = default)
    {
        // The tenant + soft-delete query filters apply to every set below, so these are the current
        // tenant's live rows only. One grouped query per resource type, not one per workspace.
        var agents = await CountByWorkspaceAsync(context.AssistantDefinitions, ct);
        var knowledge = await CountByWorkspaceAsync(context.KnowledgeBases, ct);
        var automations = await CountByWorkspaceAsync(context.Workflows, ct);
        var chatbots = await CountByWorkspaceAsync(context.Chatbots, ct);
        var connections = await CountByWorkspaceAsync(context.Applications, ct);

        return agents.Keys.Concat(knowledge.Keys).Concat(automations.Keys).Concat(chatbots.Keys).Concat(connections.Keys)
            .Distinct()
            .ToDictionary(id => id, id => new WorkspaceResourceCounts(
                agents.GetValueOrDefault(id),
                knowledge.GetValueOrDefault(id),
                automations.GetValueOrDefault(id),
                chatbots.GetValueOrDefault(id),
                connections.GetValueOrDefault(id)));
    }

    public async Task EnsureDefaultAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.TenantId ?? throw new Application.Common.Exceptions.UnauthorizedException();
        if (await context.Workspaces.AnyAsync(w => w.IsDefault, ct)) return;

        context.Workspaces.Add(Workspace.CreateDefault(tenantId));
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request created it first; IX_Workspaces_TenantId_Default rejected ours.
            context.ChangeTracker.Clear();
        }
    }

    private static async Task<Dictionary<Guid, int>> CountByWorkspaceAsync<T>(IQueryable<T> set, CancellationToken ct)
        where T : class, IWorkspaceScoped =>
        await set.Where(e => e.WorkspaceId != null)
            .GroupBy(e => e.WorkspaceId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}
