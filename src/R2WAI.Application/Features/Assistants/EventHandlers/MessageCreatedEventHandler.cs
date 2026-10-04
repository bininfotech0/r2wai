using MediatR;
using R2WAI.Domain.Events;

namespace R2WAI.Application.Features.Assistants.EventHandlers;

/// <summary>
/// Pushes a real-time notification to anyone else watching this conversation (a supervisor
/// dashboard, the same user open in a second tab) whenever a message is created — Track B Phase 6,
/// closing MessageCreatedEvent's previously-orphaned gap (raised, but with zero handlers).
/// </summary>
public class MessageCreatedEventHandler(IStreamingNotificationService notificationService)
    : INotificationHandler<MessageCreatedEvent>
{
    public Task Handle(MessageCreatedEvent notification, CancellationToken cancellationToken) =>
        notificationService.NotifyMessageCreatedAsync(
            notification.ConversationId, notification.MessageId, notification.Role.ToString(),
            notification.Content, cancellationToken);
}
