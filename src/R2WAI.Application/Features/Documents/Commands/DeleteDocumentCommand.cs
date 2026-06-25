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
    IRepository<Document> documentRepo,
    IUnitOfWork unitOfWork,
    IStorageService storageService) : IRequestHandler<DeleteDocumentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(command.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.DocumentId);

        await storageService.DeleteFileAsync(document.FilePath, cancellationToken);
        document.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
