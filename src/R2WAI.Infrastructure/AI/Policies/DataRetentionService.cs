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
public class DataRetentionService(ApplicationDbContext dbContext, ILogger<DataRetentionService> logger) : IDataRetentionService
{
    private const string PolicyType = "DataRetention";

    public async Task<DataRetentionSweepResult> RunSweepAsync(CancellationToken ct = default)
    {
        var activePolicies = await dbContext.GlobalPolicies
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive && p.Type == PolicyType)
            .ToListAsync(ct);

        int messagesPurged = 0, conversationsPurged = 0, documentsPurged = 0, tenantsSwept = 0;

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

        return new DataRetentionSweepResult(tenantsSwept, messagesPurged, conversationsPurged, documentsPurged);
    }
}
