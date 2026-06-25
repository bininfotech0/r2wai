using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record DeleteKnowledgeBaseCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteKnowledgeBaseCommandValidator : AbstractValidator<DeleteKnowledgeBaseCommand>
{
    public DeleteKnowledgeBaseCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteKnowledgeBaseCommandHandler(
    IRepository<KnowledgeBase> kbRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteKnowledgeBaseCommand, Unit>
{
    public async Task<Unit> Handle(DeleteKnowledgeBaseCommand command, CancellationToken cancellationToken)
    {
        var kb = await kbRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBase), command.Id);

        kb.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
