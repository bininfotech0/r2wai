using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace R2WAI.Api.Hubs;

[Authorize]
public class StatusHub : Hub
{
    private readonly ILogger<StatusHub> _logger;

    public StatusHub(ILogger<StatusHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

        _logger.LogInformation("StatusHub: User {UserId} connected", Context.UserIdentifier);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("StatusHub: User {UserId} disconnected", Context.UserIdentifier);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToWorkflow(string workflowInstanceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workflow_{workflowInstanceId}");
    }

    public async Task UnsubscribeFromWorkflow(string workflowInstanceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workflow_{workflowInstanceId}");
    }
}

public interface IWorkflowStatusService
{
    Task SendStepStartedAsync(Guid workflowInstanceId, string stepName, int stepIndex, CancellationToken ct = default);
    Task SendStepCompletedAsync(Guid workflowInstanceId, string stepName, int stepIndex, string? output, CancellationToken ct = default);
    Task SendStepFailedAsync(Guid workflowInstanceId, string stepName, int stepIndex, string error, CancellationToken ct = default);
    Task SendWorkflowCompletedAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task SendWorkflowFailedAsync(Guid workflowInstanceId, string error, CancellationToken ct = default);
}

public class WorkflowStatusService : IWorkflowStatusService
{
    private readonly IHubContext<StatusHub> _hubContext;

    public WorkflowStatusService(IHubContext<StatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendStepStartedAsync(Guid workflowInstanceId, string stepName, int stepIndex, CancellationToken ct)
    {
        await _hubContext.Clients.Group($"workflow_{workflowInstanceId}")
            .SendAsync("WorkflowStepStarted", stepName, stepIndex, DateTime.UtcNow.ToString("O"), ct);
    }

    public async Task SendStepCompletedAsync(Guid workflowInstanceId, string stepName, int stepIndex, string? output, CancellationToken ct)
    {
        await _hubContext.Clients.Group($"workflow_{workflowInstanceId}")
            .SendAsync("WorkflowStepCompleted", stepName, stepIndex, output, DateTime.UtcNow.ToString("O"), ct);
    }

    public async Task SendStepFailedAsync(Guid workflowInstanceId, string stepName, int stepIndex, string error, CancellationToken ct)
    {
        await _hubContext.Clients.Group($"workflow_{workflowInstanceId}")
            .SendAsync("WorkflowStepFailed", stepName, stepIndex, error, DateTime.UtcNow.ToString("O"), ct);
    }

    public async Task SendWorkflowCompletedAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        await _hubContext.Clients.Group($"workflow_{workflowInstanceId}")
            .SendAsync("WorkflowCompleted", workflowInstanceId.ToString(), DateTime.UtcNow.ToString("O"), ct);
    }

    public async Task SendWorkflowFailedAsync(Guid workflowInstanceId, string error, CancellationToken ct)
    {
        await _hubContext.Clients.Group($"workflow_{workflowInstanceId}")
            .SendAsync("WorkflowFailed", workflowInstanceId.ToString(), error, DateTime.UtcNow.ToString("O"), ct);
    }
}
