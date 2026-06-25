using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record AskDocumentCommand : IRequest<string>
{
    public Guid DocumentId { get; init; }
    public string Question { get; init; } = string.Empty;
}

public class AskDocumentCommandValidator : AbstractValidator<AskDocumentCommand>
{
    public AskDocumentCommandValidator()
    {
        RuleFor(v => v.DocumentId).NotEmpty();
        RuleFor(v => v.Question).NotEmpty().MaximumLength(2000);
    }
}

public class AskDocumentCommandHandler(
    IRepository<Document> documentRepo,
    IAIService aiService,
    IStorageService storageService) : IRequestHandler<AskDocumentCommand, string>
{
    public async Task<string> Handle(AskDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(command.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.DocumentId);

        using var stream = await storageService.DownloadFileAsync(document.FilePath, cancellationToken);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        return await aiService.AnswerQuestionAsync(command.Question, content, cancellationToken);
    }
}
