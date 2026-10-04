namespace R2WAI.Application.Common.Interfaces;

public interface IStreamingNotificationService
{
    Task SendStreamChunkAsync(Guid conversationId, string chunk, CancellationToken ct = default);
    Task SendStreamCompleteAsync(Guid conversationId, CancellationToken ct = default);

    // A repository research pass found SendMessageCommand's catch block already marks the user's
    // message Failed and persists that — but had no way to tell the client actively watching this
    // conversation's stream that it failed at all; the live connection just stopped receiving
    // chunks with no signal, distinct from (and more silent than) the correctly-fixed SSE paths
    // (AssistantsController/ChatbotsController/ChatController StreamChat/StreamMessage), which now
    // all emit a real error event.
    Task SendStreamErrorAsync(Guid conversationId, string message, CancellationToken ct = default);

    // In-chat "Checking {Capability Name}..." progress (redesign plan Phase 2) — display name only,
    // never the resolver class/endpoint/arguments. Broadcast alongside the stream, independent of
    // whichever chunk is currently being awaited (SignalR group delivery, not tied to the HTTP response).
    Task SendToolCallStartedAsync(Guid conversationId, string toolName, CancellationToken ct = default);
    Task SendToolCallCompletedAsync(Guid conversationId, string toolName, bool success, CancellationToken ct = default);

    // Real-time push for any watcher of this conversation beyond the sender (e.g. a supervisor
    // dashboard, or the same user open in a second tab) — distinct from the streaming methods above,
    // which target the one client actively streaming a reply.
    Task NotifyMessageCreatedAsync(Guid conversationId, Guid messageId, string role, string content, CancellationToken ct = default);
}
