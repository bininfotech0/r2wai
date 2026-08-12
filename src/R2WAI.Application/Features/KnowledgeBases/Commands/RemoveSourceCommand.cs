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
    IKnowledgeBaseService knowledgeBaseService) : IRequestHandler<RemoveSourceCommand, Unit>
{
    public async Task<Unit> Handle(RemoveSourceCommand command, CancellationToken cancellationToken)
    {
        await knowledgeBaseService.RemoveSourceAsync(command.SourceId, cancellationToken);
        return Unit.Value;
    }
}
