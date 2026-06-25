using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record SummarizeDocumentCommand : IRequest<DocumentSummaryDto>
{
    public Guid DocumentId { get; init; }
}

public class SummarizeDocumentCommandValidator : AbstractValidator<SummarizeDocumentCommand>
{
    public SummarizeDocumentCommandValidator()
    {
        RuleFor(v => v.DocumentId).NotEmpty();
    }
}

public class SummarizeDocumentCommandHandler(
    IRepository<Document> documentRepo,
    IAIService aiService,
    IStorageService storageService) : IRequestHandler<SummarizeDocumentCommand, DocumentSummaryDto>
{
    public async Task<DocumentSummaryDto> Handle(SummarizeDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(command.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.DocumentId);

        using var stream = await storageService.DownloadFileAsync(document.FilePath, cancellationToken);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var summary = await aiService.SummarizeTextAsync(content, ct: cancellationToken);

        return new DocumentSummaryDto
        {
            DocumentId = document.Id,
            Summary = summary,
            WordCount = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
        };
    }
}
