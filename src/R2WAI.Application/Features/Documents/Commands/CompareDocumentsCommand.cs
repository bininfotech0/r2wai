using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record CompareDocumentsCommand : IRequest<ComparisonResultDto>
{
    public Guid SourceDocumentId { get; init; }
    public Guid TargetDocumentId { get; init; }
}

public class CompareDocumentsCommandValidator : AbstractValidator<CompareDocumentsCommand>
{
    public CompareDocumentsCommandValidator()
    {
        RuleFor(v => v.SourceDocumentId).NotEmpty();
        RuleFor(v => v.TargetDocumentId).NotEmpty();
        RuleFor(v => v).Must(v => v.SourceDocumentId != v.TargetDocumentId)
            .WithMessage("Cannot compare a document with itself.");
    }
}

public class CompareDocumentsCommandHandler(
    IRepository<Document> documentRepo,
    IAIService aiService,
    IStorageService storageService) : IRequestHandler<CompareDocumentsCommand, ComparisonResultDto>
{
    public async Task<ComparisonResultDto> Handle(CompareDocumentsCommand command, CancellationToken cancellationToken)
    {
        var source = await documentRepo.GetByIdAsync(command.SourceDocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.SourceDocumentId);
        var target = await documentRepo.GetByIdAsync(command.TargetDocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.TargetDocumentId);

        var sourceContent = await ReadFileContentAsync(source.FilePath, cancellationToken);
        var targetContent = await ReadFileContentAsync(target.FilePath, cancellationToken);

        var comparison = await aiService.CompareDocumentsAsync(sourceContent, targetContent, cancellationToken);

        return new ComparisonResultDto
        {
            SourceDocumentId = source.Id,
            TargetDocumentId = target.Id,
            Comparison = comparison,
        };
    }

    private async Task<string> ReadFileContentAsync(string filePath, CancellationToken ct)
    {
        using var stream = await storageService.DownloadFileAsync(filePath, ct);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
