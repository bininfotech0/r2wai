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
    IRepository<Document> documentRepo,
    IUnitOfWork unitOfWork,
    IAIService aiService,
    IStorageService storageService,
    ILogger<ProcessDocumentCommandHandler> logger) : IRequestHandler<ProcessDocumentCommand, Unit>
{
    public async Task<Unit> Handle(ProcessDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(command.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.DocumentId);

        document.UpdateStatus(DocumentStatus.Processing);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var fileStream = await storageService.DownloadFileAsync(document.FilePath, cancellationToken);
            using var reader = new StreamReader(fileStream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                content = document.Name;
            }

            var summary = await aiService.SummarizeTextAsync(content, ct: cancellationToken);

            document.UpdateStatus(DocumentStatus.Ready);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Document {DocumentId} processed successfully.", document.Id);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process document {DocumentId}", document.Id);
            document.UpdateStatus(DocumentStatus.Failed, ex.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
