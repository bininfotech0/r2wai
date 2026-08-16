using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record ProcessDocumentCommand : IRequest<Unit>
{
    public Guid DocumentId { get; init; }
}

public class ProcessDocumentCommandValidator : AbstractValidator<ProcessDocumentCommand>
{
    public ProcessDocumentCommandValidator()
    {
        RuleFor(v => v.DocumentId)
            .NotEmpty().WithMessage("Document ID is required.");
    }
}

public class ProcessDocumentCommandHandler(
    IDocumentService documentService,
    ILogger<ProcessDocumentCommandHandler> logger) : IRequestHandler<ProcessDocumentCommand, Unit>
{
    public async Task<Unit> Handle(ProcessDocumentCommand command, CancellationToken cancellationToken)
    {
        // Delegates to IDocumentService.ProcessDocumentAsync, which chunks the document,
        // generates embeddings, and upserts them into the KB's vector collection. A separate,
        // incomplete inline implementation used to live here — it downloaded the file and asked
        // the AI to summarize it, but never chunked/embedded/indexed anything, so documents came
        // back "Ready" without ever becoming searchable via the knowledge base.
        await documentService.ProcessDocumentAsync(command.DocumentId, cancellationToken);
        logger.LogInformation("Document {DocumentId} processed successfully.", command.DocumentId);
        return Unit.Value;
    }
}
