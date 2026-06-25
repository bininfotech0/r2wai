using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record ExtractDocumentCommand : IRequest<ExtractionResultDto>
{
    public Guid DocumentId { get; init; }
    public string Schema { get; init; } = string.Empty;
}

public class ExtractDocumentCommandValidator : AbstractValidator<ExtractDocumentCommand>
{
    public ExtractDocumentCommandValidator()
    {
        RuleFor(v => v.DocumentId).NotEmpty();
        RuleFor(v => v.Schema).NotEmpty().WithMessage("Extraction schema is required.");
    }
}

public class ExtractDocumentCommandHandler(
    IRepository<Document> documentRepo,
    IAIService aiService,
    IStorageService storageService) : IRequestHandler<ExtractDocumentCommand, ExtractionResultDto>
{
    public async Task<ExtractionResultDto> Handle(ExtractDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(command.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.DocumentId);

        using var stream = await storageService.DownloadFileAsync(document.FilePath, cancellationToken);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var extracted = await aiService.ExtractDataAsync(content, command.Schema, cancellationToken);

        return new ExtractionResultDto
        {
            DocumentId = document.Id,
            ExtractedData = extracted,
            Schema = command.Schema,
        };
    }
}
