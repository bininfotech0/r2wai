using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Domain.Entities;
using R2WAI.Domain.Enums;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Infrastructure.Services.BackgroundJobs;

/// <summary>
/// Polls BackgroundJobs for due Pending rows and dispatches each to the IBackgroundJobHandler
/// matching its JobType, with retry/backoff/dead-letter handled by BackgroundJob.MarkFailed --
/// replaces the old BackgroundTaskProcessor (an in-memory Channel reader with no persistence,
/// retry, or dead-letter path at all; every queued item was lost on restart/crash).
///
/// Claims a job via an atomic conditional UPDATE (Status: Pending -> Processing, WHERE Status =
/// Pending) before processing it -- unlike WorkflowDelayResumeBackgroundService's equivalent
/// sweep (no claim at all), this specifically guards against two API replicas picking up the
/// same row: ExecuteUpdateAsync's affected-row count tells us whether *this* replica actually won
/// the claim, so a losing replica just skips the row instead of double-sending an approval email
/// or double-processing a document.
///
/// The claim also grants a time-boxed lease (implementation plan Phase 5): a job whose claiming
/// replica crashes between the claim and MarkSucceeded/MarkFailed used to stay stuck in
/// Processing forever (the due-jobs query only ever looked at Pending). Now the same query also
/// picks up a Processing row whose LeaseExpiresAt has passed, and the claim UPDATE re-grants the
/// lease to whichever replica wins it next -- same atomic-affected-row-count idiom, just a wider
/// WHERE clause.
/// </summary>
public sealed class BackgroundJobProcessor(
    IServiceProvider serviceProvider, ILogger<BackgroundJobProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    // Long enough that every real handler here (send an email, kick off document processing) has
    // finished well within it; short enough that a genuinely crashed job doesn't sit stuck for
    // hours before another replica reclaims it.
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    // Diagnostic only (BackgroundJob.LeaseOwner is never read back for correctness -- only
    // LeaseExpiresAt is), so this only needs to be unique per process, not durable.
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
                await ProcessDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job processor sweep failed");
            }
        }

        logger.LogInformation("Background job processor stopped");
    }

    private async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var handlers = scope.ServiceProvider.GetServices<IBackgroundJobHandler>()
            .ToDictionary(h => h.JobType, StringComparer.Ordinal);

        var now = DateTime.UtcNow;

        // A real Expression<> variable, not a plain method call -- EF Core can only translate a
        // query predicate to SQL when it's an expression tree it can inspect, not an arbitrary
        // compiled C# method. Building it once and reusing it in both the SELECT and the claim
        // UPDATE's WHERE also guarantees the two can never disagree about what's claimable.
        Expression<Func<BackgroundJob, bool>> isClaimable = j =>
            (j.Status == BackgroundJobStatus.Pending && j.NextAttemptAt <= now) ||
            (j.Status == BackgroundJobStatus.Processing && j.LeaseExpiresAt != null && j.LeaseExpiresAt <= now);

        var dueIds = await dbContext.BackgroundJobs
            .Where(isClaimable)
            .OrderBy(j => j.NextAttemptAt)
            .Take(BatchSize)
            .Select(j => j.Id)
            .ToListAsync(ct);

        foreach (var jobId in dueIds)
        {
            var leaseExpiresAt = DateTime.UtcNow.Add(LeaseDuration);
            var claimed = await dbContext.BackgroundJobs
                .Where(j => j.Id == jobId)
                .Where(isClaimable)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, BackgroundJobStatus.Processing)
                    .SetProperty(j => j.LeaseOwner, _instanceId)
                    .SetProperty(j => j.LeaseExpiresAt, leaseExpiresAt), ct);

            if (claimed == 0)
                continue; // another replica claimed it first

            await ProcessOneAsync(dbContext, handlers, jobId, ct);
        }
    }

    private async Task ProcessOneAsync(
        ApplicationDbContext dbContext, IReadOnlyDictionary<string, IBackgroundJobHandler> handlers,
        Guid jobId, CancellationToken ct)
    {
        var job = await dbContext.BackgroundJobs.FirstAsync(j => j.Id == jobId, ct);

        if (!handlers.TryGetValue(job.JobType, out var handler))
        {
            logger.LogError("No IBackgroundJobHandler registered for job type {JobType} (job {JobId}) -- dead-lettering", job.JobType, jobId);
            job.MarkUnroutable($"No handler registered for job type '{job.JobType}'");
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        try
        {
            await handler.HandleAsync(job.PayloadJson, ct);
            job.MarkSucceeded();
            logger.LogInformation("Background job {JobId} ({JobType}) succeeded", jobId, job.JobType);
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            if (job.Status == BackgroundJobStatus.DeadLettered)
                logger.LogError(ex, "Background job {JobId} ({JobType}) dead-lettered after {Attempts} attempts", jobId, job.JobType, job.Attempts);
            else
                logger.LogWarning(ex, "Background job {JobId} ({JobType}) failed, attempt {Attempts}/{MaxAttempts}, retrying at {NextAttemptAt}",
                    jobId, job.JobType, job.Attempts, job.MaxAttempts, job.NextAttemptAt);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
