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
}
