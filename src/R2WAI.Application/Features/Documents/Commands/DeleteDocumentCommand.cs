using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record DeleteDocumentCommand : IRequest<Unit>
{
    public Guid DocumentId { get; init; }
}

public class DeleteDocumentCommandValidator : AbstractValidator<DeleteDocumentCommand>
{
    public DeleteDocumentCommandValidator()
    {
        RuleFor(v => v.DocumentId)
            .NotEmpty().WithMessage("Document ID is required.");
    }
}

public class DeleteDocumentCommandHandler(
    IDocumentService documentService) : IRequestHandler<DeleteDocumentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        // Routed through IDocumentService, which owns the full teardown: the stored file, the
        // document's vector chunks, then the soft delete. This handler used to do only the first and
        // third, which left every embedding of the document's text in the collection — still matching
        // knowledge-base searches and still being fed to models after the user deleted the document.
        // It also passed FilePath straight to DeleteFileAsync with no null check.
        await documentService.DeleteDocumentAsync(command.DocumentId, cancellationToken);

        return Unit.Value;
    }
}
