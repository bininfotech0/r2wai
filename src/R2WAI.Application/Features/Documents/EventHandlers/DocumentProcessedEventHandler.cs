using MediatR;

namespace R2WAI.Application.Features.Documents.EventHandlers;

/// <summary>
/// Notifies the uploading user once their document finishes processing (or fails) — Track B Phase 6,
/// closing DocumentProcessedEvent's previously-orphaned gap (raised, but with zero handlers).
/// </summary>
public class DocumentProcessedEventHandler(INotificationService notificationService)
    : INotificationHandler<DocumentProcessedEvent>
{
    public Task Handle(DocumentProcessedEvent notification, CancellationToken cancellationToken) =>
        notification.Success
            ? notificationService.SendAsync(
                notification.UserId.ToString(),
                "Document ready",
                $"\"{notification.Name}\" finished processing" + (notification.PageCount is { } n ? $" ({n} chunks indexed)." : "."),
                type: "document",
                ct: cancellationToken)
            : notificationService.SendAsync(
                notification.UserId.ToString(),
                "Document processing failed",
                $"\"{notification.Name}\" could not be processed: {notification.Error ?? "unknown error"}.",
                type: "document",
                ct: cancellationToken);
}
