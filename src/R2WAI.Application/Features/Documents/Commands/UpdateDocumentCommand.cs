using FluentValidation;

namespace R2WAI.Application.Features.Documents.Commands;

public record UpdateDocumentCommand : IRequest<DocumentDto>
{
    public Guid DocumentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? KnowledgeBaseId { get; init; }
}

public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(v => v.DocumentId)
            .NotEmpty().WithMessage("Document ID is required.");
        RuleFor(v => v.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(500).WithMessage("Name must not exceed 500 characters.");
    }
}

public class UpdateDocumentCommandHandler(
    IRepository<Document> documentRepo,
    IRepository<KnowledgeBase> knowledgeBaseRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UpdateDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documentRepo.GetByIdAsync(command.DocumentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), command.DocumentId);

        if (command.KnowledgeBaseId.HasValue)
        {
            var knowledgeBase = await knowledgeBaseRepo.GetByIdAsync(command.KnowledgeBaseId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(KnowledgeBase), command.KnowledgeBaseId.Value);
        }

        document.UpdateDetails(command.Name, command.Description, command.KnowledgeBaseId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<DocumentDto>(document);
    }
}
