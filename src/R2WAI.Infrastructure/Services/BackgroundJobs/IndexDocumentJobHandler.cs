using System.Text.Json;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;

namespace R2WAI.Infrastructure.Services.BackgroundJobs;

public sealed class IndexDocumentJobHandler(IDocumentService documentService) : IBackgroundJobHandler
{
    public string JobType => BackgroundJobTypes.IndexDocument;

    public Task HandleAsync(string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<IndexDocumentJobPayload>(payloadJson)
            ?? throw new InvalidOperationException($"Malformed {nameof(IndexDocumentJobPayload)}");

        // ProcessDocumentAsync already records a DocumentProcessedEvent(success: false) for
        // failures within its own try/catch. Anything that throws past that point (e.g. the
        // document row itself is gone) propagates to the processor, which retries/dead-letters
        // it like any other job -- previously this was only ever logged once, with no retry.
        return documentService.ProcessDocumentAsync(payload.DocumentId, ct, expectedTenantId: payload.TenantId);
    }
}
