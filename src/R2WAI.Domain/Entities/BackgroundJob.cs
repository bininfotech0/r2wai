using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A durable, retryable unit of background work — replaces the old purely in-memory
/// IBackgroundTaskQueue (a bounded Channel of closures), which silently dropped every queued
/// item on a process restart/crash and had no retry or dead-letter path at all. Pending/due jobs
/// are claimed by BackgroundJobProcessor via an atomic conditional UPDATE (Status: Pending ->
/// Processing, WHERE Status = Pending), not through an entity method, so two API replicas
/// racing on the same row can't both pick it up.
/// </summary>
public sealed class BackgroundJob : BaseEntity<Guid>
{
    public string JobType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public BackgroundJobStatus Status { get; private set; } = BackgroundJobStatus.Pending;
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    // Implementation plan Phase 5: without these, a claimed-then-crashed job (the atomic claim's
    // UPDATE ran, Status is Processing, but the process died before ever reaching MarkSucceeded/
    // MarkFailed) is invisible to BackgroundJobProcessor's due-jobs query forever — it only looks
    // at Status == Pending, so a stuck Processing row with no lease/expiry is never retried or
    // dead-lettered again. LeaseOwner is diagnostic only (which processor instance last claimed
    // this); correctness comes entirely from LeaseExpiresAt.
    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }

    // Backoff schedule: 30s, 2m, 10m, 30m, 1h -- deliberately front-loaded (most transient
    // failures here -- an SMTP hiccup, a slow/rate-limited embedding call -- resolve within the
    // first retry or two) rather than a smooth exponential curve out to hours immediately.
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)
    ];

    private BackgroundJob() { }

    public BackgroundJob(Guid id, string jobType, string payloadJson, int maxAttempts = 5)
    {
        Id = id;
        JobType = jobType;
        PayloadJson = payloadJson;
        MaxAttempts = maxAttempts;
        NextAttemptAt = DateTime.UtcNow;
    }

    public void MarkSucceeded()
    {
        Status = BackgroundJobStatus.Succeeded;
        ProcessedAt = DateTime.UtcNow;
        ClearLease();
        MarkAsModified();
    }

    /// <summary>
    /// Skips straight to DeadLettered without consuming a retry -- for failures no retry could
    /// ever fix (e.g. no handler registered for this JobType), as opposed to MarkFailed's
    /// transient-failure backoff-and-retry path.
    /// </summary>
    public void MarkUnroutable(string reason)
    {
        Attempts++;
        LastError = reason.Length > 2000 ? reason[..2000] : reason;
        Status = BackgroundJobStatus.DeadLettered;
        ProcessedAt = DateTime.UtcNow;
        ClearLease();
        MarkAsModified();
    }

    /// <summary>
    /// Records a failed attempt. Re-queues (Status back to Pending, NextAttemptAt pushed out per
    /// the backoff schedule) while Attempts is still under MaxAttempts; DeadLetters once it isn't
    /// -- at which point it stops being picked up by the processor's due-jobs query and needs a
    /// human/admin action to requeue, matching the "dead letter" semantics used elsewhere in this
    /// codebase's queueing (this is the first real one).
    /// </summary>
    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;

        if (Attempts >= MaxAttempts)
        {
            Status = BackgroundJobStatus.DeadLettered;
            ProcessedAt = DateTime.UtcNow;
        }
        else
        {
            Status = BackgroundJobStatus.Pending;
            var delay = BackoffSchedule[Math.Min(Attempts - 1, BackoffSchedule.Length - 1)];
            NextAttemptAt = DateTime.UtcNow.Add(delay);
        }

        ClearLease();
        MarkAsModified();
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }
}
