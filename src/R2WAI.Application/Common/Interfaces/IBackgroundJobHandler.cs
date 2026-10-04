namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Handles one BackgroundJob.JobType. Registered as scoped services and resolved by JobType at
/// dispatch time (BackgroundJobProcessor), one handler per job type -- see NotifyApproversJobHandler
/// and IndexDocumentJobHandler for the two real implementations.
/// </summary>
public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task HandleAsync(string payloadJson, CancellationToken ct);
}
