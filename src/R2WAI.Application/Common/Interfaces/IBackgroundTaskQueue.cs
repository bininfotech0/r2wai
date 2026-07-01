namespace R2WAI.Application.Common.Interfaces;

public interface IBackgroundTaskQueue
{
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken ct = default);
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}
