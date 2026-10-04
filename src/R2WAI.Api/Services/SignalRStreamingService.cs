using Microsoft.AspNetCore.SignalR;
using R2WAI.Api.Hubs;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Services;

public class SignalRStreamingService : IStreamingNotificationService
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRStreamingService(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendStreamChunkAsync(Guid conversationId, string chunk, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("StreamChunk", chunk, ct);
    }

    public async Task SendStreamCompleteAsync(Guid conversationId, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("StreamComplete", ct);
    }

    public async Task SendStreamErrorAsync(Guid conversationId, string message, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("StreamError", new { message }, ct);
    }

    public async Task SendToolCallStartedAsync(Guid conversationId, string toolName, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("ToolCallStarted", new { toolName }, ct);
    }

    public async Task SendToolCallCompletedAsync(Guid conversationId, string toolName, bool success, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("ToolCallCompleted", new { toolName, success }, ct);
    }

    public async Task NotifyMessageCreatedAsync(Guid conversationId, Guid messageId, string role, string content, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("MessageCreated", new
        {
            conversationId,
            messageId,
            role,
            content
        }, ct);
    }
}
