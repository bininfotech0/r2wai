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
    IKnowledgeBaseService knowledgeBaseService) : IRequestHandler<DeleteKnowledgeBaseCommand, Unit>
{
    public async Task<Unit> Handle(DeleteKnowledgeBaseCommand command, CancellationToken cancellationToken)
    {
        // Routed through IKnowledgeBaseService rather than repo.SoftDelete() directly: the service
        // purges the vector collection before soft-deleting the row. The previous handler only
        // soft-deleted, so every chunk of the knowledge base stayed in vector_embeddings — still
        // matching searches, still being handed to models, and never garbage-collected.
        await knowledgeBaseService.DeleteKnowledgeBaseAsync(command.Id, cancellationToken);

        return Unit.Value;
    }
}
