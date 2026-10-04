using Moq;
using R2WAI.Application.Common.Models;
using R2WAI.Application.Features.Documents.EventHandlers;

namespace R2WAI.Application.Tests.Handlers;

public class DocumentUploadedEventHandlerTests
{
    [Fact]
    public async Task Handle_EnqueuesIndexDocumentJobWithTheUploadedDocumentId()
    {
        var jobQueueMock = new Mock<IBackgroundJobQueue>();
        var handler = new DocumentUploadedEventHandler(jobQueueMock.Object);
        var documentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var evt = new DocumentUploadedEvent(documentId, tenantId, Guid.NewGuid(), "report.pdf", DocumentType.PDF, 1024);

        await handler.Handle(evt, CancellationToken.None);

        // TenantId must round-trip onto the payload — IndexDocumentJobHandler runs from
        // BackgroundJobProcessor (no ambient tenant claim) and needs it to look the document up
        // under the fail-closed tenant filter (P0-5); losing it here means every uploaded document
        // silently fails to index (see IndexDocumentJobHandlerTests for that side of the proof).
        jobQueueMock.Verify(q => q.EnqueueAsync(
            BackgroundJobTypes.IndexDocument,
            It.Is<IndexDocumentJobPayload>(p => p.DocumentId == documentId && p.TenantId == tenantId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
