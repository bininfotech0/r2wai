using Moq;
using R2WAI.Application.Features.Assistants.EventHandlers;

namespace R2WAI.Application.Tests.Handlers;

public class MessageCreatedEventHandlerTests
{
    [Fact]
    public async Task Handle_PushesNotificationWithConversationAndMessageDetails()
    {
        var notificationServiceMock = new Mock<IStreamingNotificationService>();
        var handler = new MessageCreatedEventHandler(notificationServiceMock.Object);

        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var evt = new MessageCreatedEvent(messageId, conversationId, Guid.NewGuid(), Guid.NewGuid(), "Hello there", MessageRole.Assistant);

        await handler.Handle(evt, CancellationToken.None);

        notificationServiceMock.Verify(s => s.NotifyMessageCreatedAsync(
            conversationId, messageId, "Assistant", "Hello there", It.IsAny<CancellationToken>()), Times.Once);
    }
}
