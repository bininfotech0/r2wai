using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using R2WAI.Infrastructure.Persistence;

namespace R2WAI.Api.Hubs;

[Authorize]
public class StatusHub : Hub
{
    private readonly ILogger<StatusHub> _logger;
    private readonly ApplicationDbContext _dbContext;

    public StatusHub(ILogger<StatusHub> logger, ApplicationDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
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
        // A run's step names, outputs and errors are tenant data. This used to add ANY authenticated
        // caller to the run's group, so a user from another tenant who knew (or was handed) an instance
        // id could listen to it. Only a caller from the run's own tenant may subscribe; a refusal is
        // silent (like ChatHub.JoinConversation) so it doesn't confirm whether an id exists.
        if (!Guid.TryParse(workflowInstanceId, out var instanceId))
            return;

        if (!Guid.TryParse(Context.User?.FindFirst("tenant_id")?.Value, out var tenantId))
            return;

        // IgnoreQueryFilters: a SignalR Hub method invocation is not a normal per-request HTTP
        // pipeline consumer — IHttpContextAccessor.HttpContext (what the DbContext's own ambient
        // tenant filter reads) is not reliably populated here even on an authenticated connection
        // (a known ASP.NET Core SignalR/WebSocket gap, not specific to this codebase). The explicit
        // `i.TenantId == tenantId` clause above/below, driven by Context.User (SignalR's own reliable
        // claims source), is the real and only tenant check this method needs — same pattern every
        // background sweeper already uses for the same underlying reason (no reliable ambient
        // HttpContext). Confirmed by this fix: before it, P0-5's tenant-filter fail-closed change
        // broke same-tenant subscriptions too, not just cross-tenant ones.
        var ownsRun = await _dbContext.WorkflowInstances.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(i => i.Id == instanceId && i.TenantId == tenantId);
        if (!ownsRun)
        {
            _logger.LogWarning("StatusHub: user {UserId} tried to subscribe to workflow run {InstanceId} outside their tenant",
                Context.UserIdentifier, instanceId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"workflow_{instanceId}");
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
