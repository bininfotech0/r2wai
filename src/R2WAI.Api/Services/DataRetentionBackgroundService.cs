using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Services;

/// <summary>
/// Thin polling loop — the actual sweep logic lives in IDataRetentionService (Infrastructure), same
/// split as ApprovalService/EscalationBackgroundService, so it's independently callable (e.g. the
/// admin on-demand endpoint) and testable without a running background loop.
/// </summary>
public sealed class DataRetentionBackgroundService(
    IServiceProvider serviceProvider, ILogger<DataRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Data retention background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);

                using var scope = serviceProvider.CreateScope();
                var dataRetentionService = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
                await dataRetentionService.RunSweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data retention sweep failed");
            }
        }

        logger.LogInformation("Data retention background service stopped");
    }
}
