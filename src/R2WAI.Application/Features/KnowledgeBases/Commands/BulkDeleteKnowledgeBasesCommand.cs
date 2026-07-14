using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record BulkDeleteKnowledgeBasesCommand : IRequest<Unit>
{
    public Guid[] Ids { get; init; } = [];
}

public class BulkDeleteKnowledgeBasesCommandValidator : AbstractValidator<BulkDeleteKnowledgeBasesCommand>
{
    public BulkDeleteKnowledgeBasesCommandValidator()
    {
        RuleFor(v => v.Ids).NotEmpty();
    }
}

public class BulkDeleteKnowledgeBasesCommandHandler(
    IRepository<KnowledgeBase> kbRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<BulkDeleteKnowledgeBasesCommand, Unit>
{
    public async Task<Unit> Handle(BulkDeleteKnowledgeBasesCommand command, CancellationToken cancellationToken)
    {
        foreach (var id in command.Ids)
        {
            var kb = await kbRepo.GetByIdAsync(id, cancellationToken);
            kb?.SoftDelete();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
