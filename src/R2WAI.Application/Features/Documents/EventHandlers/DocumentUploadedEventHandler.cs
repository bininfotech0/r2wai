namespace R2WAI.Application.Features.Documents.EventHandlers;

/// <summary>
/// Enqueues indexing for a just-uploaded document — Track B Phase 6, closing a real gap: uploaded
/// documents were persisted but never automatically chunked/embedded/indexed; only a manual
/// POST /documents/{id}/process call (or a knowledge base "Reindex") ever triggered it. Backgrounded
/// via IBackgroundJobQueue rather than awaited inline, since indexing (chunking + embedding calls)
/// can take tens of seconds and the upload response shouldn't block on it — matches the pattern
/// ApprovalService already uses for its own background follow-up work. Durable and retried on
/// failure (IndexDocumentJobHandler/BackgroundJobProcessor) rather than a fire-and-forget in-memory
/// task, so a transient AI-provider blip during embedding no longer needs a manual re-process call.
/// </summary>
public class DocumentUploadedEventHandler(IBackgroundJobQueue jobQueue)
    : INotificationHandler<DocumentUploadedEvent>
{
    public Task Handle(DocumentUploadedEvent notification, CancellationToken cancellationToken) =>
        jobQueue.EnqueueAsync(BackgroundJobTypes.IndexDocument, new IndexDocumentJobPayload(notification.DocumentId, notification.TenantId), cancellationToken);
}
