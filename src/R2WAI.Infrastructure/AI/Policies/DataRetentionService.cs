using Microsoft.EntityFrameworkCore;
using R2WAI.Application.Common.Interfaces;
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

            var oldMessages = await dbContext.Messages
                .IgnoreQueryFilters()
                .Where(m => !m.IsDeleted && m.TenantId == tenantId && m.CreatedAt < cutoff)
                .ToListAsync(ct);
            foreach (var message in oldMessages)
                message.SoftDelete();

            var oldConversations = await dbContext.Conversations
                .IgnoreQueryFilters()
                .Where(c => !c.IsDeleted && c.TenantId == tenantId && c.CreatedAt < cutoff)
                .ToListAsync(ct);
            foreach (var conversation in oldConversations)
                conversation.SoftDelete();

            var oldDocuments = await dbContext.Documents
                .IgnoreQueryFilters()
                .Where(d => !d.IsDeleted && d.TenantId == tenantId && d.CreatedAt < cutoff)
                .ToListAsync(ct);
            foreach (var document in oldDocuments)
                document.SoftDelete();

            if (cutoff > sessionCutoff)
                sessionTurnsPurged += await PurgeChatbotSessionTurnsAsync(tenantId, cutoff, ct);

            messagesPurged += oldMessages.Count;
            conversationsPurged += oldConversations.Count;
            documentsPurged += oldDocuments.Count;

            if (oldMessages.Count > 0 || oldConversations.Count > 0 || oldDocuments.Count > 0)
            {
                logger.LogInformation(
                    "Data retention: tenant {TenantId} purged {Messages} messages, {Conversations} conversations, {Documents} documents older than {RetentionDays}d",
                    tenantId, oldMessages.Count, oldConversations.Count, oldDocuments.Count, retentionDays);
            }
        }

        if (activePolicies.Count > 0)
            await dbContext.SaveChangesAsync(ct);

        if (sessionTurnsPurged > 0)
            logger.LogInformation("Data retention: purged {Count} chatbot session turns", sessionTurnsPurged);

        return new DataRetentionSweepResult(tenantsSwept, messagesPurged, conversationsPurged, documentsPurged, sessionTurnsPurged);
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
