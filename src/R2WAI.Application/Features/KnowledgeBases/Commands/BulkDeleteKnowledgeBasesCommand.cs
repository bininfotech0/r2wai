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
    IKnowledgeBaseService knowledgeBaseService,
    ILogger<BulkDeleteKnowledgeBasesCommandHandler> logger) : IRequestHandler<BulkDeleteKnowledgeBasesCommand, Unit>
{
    public async Task<Unit> Handle(BulkDeleteKnowledgeBasesCommand command, CancellationToken cancellationToken)
    {
        foreach (var id in command.Ids)
        {
            // Same reasoning as the single delete: the service is what purges the vector collection.
            // One id in a bulk selection may already be gone (or belong to another tenant, in which
            // case the ambient tenant filter makes it invisible) — skip those rather than failing the
            // whole batch, which is what the previous soft-delete-only loop effectively did.
            try
            {
                await knowledgeBaseService.DeleteKnowledgeBaseAsync(id, cancellationToken);
            }
            catch (NotFoundException ex)
            {
                logger.LogInformation("Skipping bulk delete of knowledge base {Id}: {Message}", id, ex.Message);
            }
        }

        return Unit.Value;
    }
}
