namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Runs the "DataRetention" GlobalPolicy sweep (Policy Engine, Phase 2) — soft-deletes each opted-in
/// tenant's Conversation/Message/Document rows older than its configured retentionDays. A tenant
/// with no active DataRetention policy is untouched. Invoked on a schedule by
/// DataRetentionBackgroundService, and directly via POST /api/v1/admin/data-retention/run for
/// on-demand/testing use.
/// </summary>
public interface IDataRetentionService
{
    Task<DataRetentionSweepResult> RunSweepAsync(CancellationToken ct = default);
}

public record DataRetentionSweepResult(int TenantsSwept, int MessagesPurged, int ConversationsPurged, int DocumentsPurged);
