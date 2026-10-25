using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Common;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.AI.Policies;

/// <summary>
/// Runs cross-tenant, so every query here uses .IgnoreQueryFilters() + an explicit manual
/// TenantId/!IsDeleted filter — same pattern used by every cross-tenant background sweep in this
/// codebase (e.g. ApprovalService.EscalateOverdueAsync): the ambient tenant filter can't be trusted
/// for a sweep that must see every tenant's policy regardless of who/what triggered it (a background
/// loop with no HttpContext, or an admin's own authenticated request via the on-demand endpoint).
/// </summary>
public class DataRetentionService(ApplicationDbContext dbContext, IConfiguration configuration, ILogger<DataRetentionService> logger) : IDataRetentionService
{
    private const string PolicyType = "DataRetention";
    private const int BatchSize = 500;

    public async Task<DataRetentionSweepResult> RunSweepAsync(CancellationToken ct = default)
    {
        var activePolicies = await dbContext.GlobalPolicies
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive && p.Type == PolicyType)
            .ToListAsync(ct);

        int messagesPurged = 0, conversationsPurged = 0, documentsPurged = 0, tenantsSwept = 0;

        // Chatbot session memory has its own short default lifetime, policy or not: it is anonymous
        // visitor text kept only so a live conversation can refer back to itself.
        var sessionRetentionDays = Math.Clamp(configuration.GetValue("Chatbots:SessionMemory:RetentionDays", 7), 1, 365);
        var sessionCutoff = DateTime.UtcNow.AddDays(-sessionRetentionDays);
        var sessionTurnsPurged = await PurgeChatbotSessionTurnsAsync(null, sessionCutoff, ct);

        foreach (var policy in activePolicies)
        {
            var retentionDays = DataRetentionPolicyEvaluator.TryParseRetentionDays(policy.Content);
            if (retentionDays is null) continue;

            tenantsSwept++;
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays.Value);
            var tenantId = policy.TenantId;

            var oldMessages = await SoftDeleteExpiredAsync(dbContext.Messages
                .IgnoreQueryFilters()
                .Where(m => !m.IsDeleted && m.TenantId == tenantId && m.CreatedAt < cutoff), ct);

            var oldConversations = await SoftDeleteExpiredAsync(dbContext.Conversations
                .IgnoreQueryFilters()
                .Where(c => !c.IsDeleted && c.TenantId == tenantId && c.CreatedAt < cutoff), ct);

            var oldDocuments = await SoftDeleteExpiredAsync(dbContext.Documents
                .IgnoreQueryFilters()
                .Where(d => !d.IsDeleted && d.TenantId == tenantId && d.CreatedAt < cutoff), ct);

            if (cutoff > sessionCutoff)
                sessionTurnsPurged += await PurgeChatbotSessionTurnsAsync(tenantId, cutoff, ct);

            messagesPurged += oldMessages;
            conversationsPurged += oldConversations;
            documentsPurged += oldDocuments;

            if (oldMessages > 0 || oldConversations > 0 || oldDocuments > 0)
            {
                logger.LogInformation(
                    "Data retention: tenant {TenantId} purged {Messages} messages, {Conversations} conversations, {Documents} documents older than {RetentionDays}d",
                    tenantId, oldMessages, oldConversations, oldDocuments, retentionDays);
            }
        }

        if (sessionTurnsPurged > 0)
            logger.LogInformation("Data retention: purged {Count} chatbot session turns", sessionTurnsPurged);

        return new DataRetentionSweepResult(tenantsSwept, messagesPurged, conversationsPurged, documentsPurged, sessionTurnsPurged);
    }

    // Retention can target years of user data on a mature tenant. Loading every expired row into a
    // single tracked DbContext made a routine hourly sweep grow with the full historical dataset and
    // could exhaust API memory. Process stable, bounded batches; conditional updates also let API
    // replicas safely race the same retention window without double-processing rows.
    private async Task<int> SoftDeleteExpiredAsync<TEntity>(IQueryable<TEntity> expiredRows, CancellationToken ct)
        where TEntity : BaseEntity<Guid>
    {
        var total = 0;
        while (true)
        {
            var ids = await expiredRows
                .Where(row => !row.IsDeleted)
                .OrderBy(row => row.CreatedAt)
                .ThenBy(row => row.Id)
                .Take(BatchSize)
                .Select(row => row.Id)
                .ToListAsync(ct);

            if (ids.Count == 0)
                return total;

            if (dbContext.Database.IsRelational())
            {
                var now = DateTime.UtcNow;
                var updated = await expiredRows
                    .Where(row => !row.IsDeleted && ids.Contains(row.Id))
                    .ExecuteUpdateAsync(updates => updates
                        .SetProperty(row => row.IsDeleted, true)
                        .SetProperty(row => row.ModifiedAt, now), ct);
                total += updated;
                continue;
            }

            var batch = await expiredRows
                .Where(row => !row.IsDeleted && ids.Contains(row.Id))
                .ToListAsync(ct);
            if (batch.Count == 0)
                continue;

            foreach (var row in batch)
                row.SoftDelete();

            await dbContext.SaveChangesAsync(ct);
            dbContext.ChangeTracker.Clear();
            total += batch.Count;
        }
    }

    // Hard delete: soft-deleted session text would still be anonymous visitor data sitting in the
    // database. ExecuteDeleteAsync is relational-only, so the InMemory test provider takes the
    // load-and-remove path.
    private async Task<int> PurgeChatbotSessionTurnsAsync(Guid? tenantId, DateTime cutoff, CancellationToken ct)
    {
        var query = dbContext.ChatbotSessionTurns
            .IgnoreQueryFilters()
            .Where(t => t.CreatedAt < cutoff && (tenantId == null || t.TenantId == tenantId));

        if (dbContext.Database.IsRelational())
            return await query.ExecuteDeleteAsync(ct);

        var rows = await query.ToListAsync(ct);
        dbContext.ChatbotSessionTurns.RemoveRange(rows);
        await dbContext.SaveChangesAsync(ct);
        return rows.Count;
    }
}
