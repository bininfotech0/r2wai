using Moq;
using R2WAI.Application.Features.Documents.EventHandlers;

namespace R2WAI.Application.Tests.Handlers;

public class DocumentProcessedEventHandlerTests
{
    [Fact]
    public async Task Handle_SuccessfulProcessing_SendsReadyNotification()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var handler = new DocumentProcessedEventHandler(notificationServiceMock.Object);

        var userId = Guid.NewGuid();
        var evt = new DocumentProcessedEvent(Guid.NewGuid(), Guid.NewGuid(), userId, "report.pdf", true, pageCount: 5);

        await handler.Handle(evt, CancellationToken.None);

        notificationServiceMock.Verify(s => s.SendAsync(
            userId.ToString(),
            "Document ready",
            It.Is<string>(m => m.Contains("report.pdf") && m.Contains("5 chunks")),
            "document",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FailedProcessing_SendsFailureNotificationWithError()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var handler = new DocumentProcessedEventHandler(notificationServiceMock.Object);

        var userId = Guid.NewGuid();
        var evt = new DocumentProcessedEvent(Guid.NewGuid(), Guid.NewGuid(), userId, "bad.pdf", false, error: "corrupt file");

        await handler.Handle(evt, CancellationToken.None);

        notificationServiceMock.Verify(s => s.SendAsync(
            userId.ToString(),
            "Document processing failed",
            It.Is<string>(m => m.Contains("bad.pdf") && m.Contains("corrupt file")),
            "document",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
