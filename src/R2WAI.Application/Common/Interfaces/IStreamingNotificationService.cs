namespace R2WAI.Application.Common.Interfaces;

public interface IStreamingNotificationService
{
    Task SendStreamChunkAsync(Guid conversationId, string chunk, CancellationToken ct = default);
    Task SendStreamCompleteAsync(Guid conversationId, CancellationToken ct = default);
}
