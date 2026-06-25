using FluentValidation;

namespace R2WAI.Application.Features.KnowledgeBases.Commands;

public record RemoveSourceCommand : IRequest<Unit>
{
    public Guid SourceId { get; init; }
}

public class RemoveSourceCommandValidator : AbstractValidator<RemoveSourceCommand>
{
    public RemoveSourceCommandValidator()
    {
        RuleFor(v => v.SourceId).NotEmpty();
    }
}

public class RemoveSourceCommandHandler(
    IRepository<KnowledgeBaseSource> sourceRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveSourceCommand, Unit>
{
    public async Task<Unit> Handle(RemoveSourceCommand command, CancellationToken cancellationToken)
    {
        var source = await sourceRepo.GetByIdAsync(command.SourceId, cancellationToken)
            ?? throw new NotFoundException(nameof(KnowledgeBaseSource), command.SourceId);

        sourceRepo.Delete(source);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
