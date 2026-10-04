namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Durably enqueues a named job type with a JSON-serializable payload -- see BackgroundJob
/// (Domain) for why this replaced the old in-memory IBackgroundTaskQueue. jobType must match an
/// IBackgroundJobHandler.JobType registered in DI; the processor logs and dead-letters
/// immediately if no handler is found rather than crash-looping on an unroutable job.
/// </summary>
public interface IBackgroundJobQueue
{
    Task EnqueueAsync(string jobType, object payload, CancellationToken ct = default);
}
