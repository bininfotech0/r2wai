using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.Services;

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });
    }

    public async ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem, ct);
    }

    public async ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct)
    {
        return await _queue.Reader.ReadAsync(ct);
    }
}

public sealed class BackgroundTaskProcessor : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundTaskProcessor> _logger;

    public BackgroundTaskProcessor(IBackgroundTaskQueue queue, IServiceProvider serviceProvider,
        ILogger<BackgroundTaskProcessor> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background task processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _queue.DequeueAsync(stoppingToken);
                using var scope = _serviceProvider.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background task failed");
            }
        }

        _logger.LogInformation("Background task processor stopped");
    }
}
