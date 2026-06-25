using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace R2WAI.Infrastructure.Services;

public sealed class EscalationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EscalationBackgroundService> _logger;

    public EscalationBackgroundService(IServiceProvider serviceProvider, ILogger<EscalationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Escalation background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();
                await approvalService.EscalateOverdueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Escalation check failed");
            }
        }

        _logger.LogInformation("Escalation background service stopped");
    }
}
