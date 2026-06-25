namespace R2WAI.Application.Features.Integrations.Commands;

public class DeleteIntegrationCommandHandler(
    IRepository<ToolDefinition> repo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteIntegrationCommand>
{
    public async Task Handle(DeleteIntegrationCommand command, CancellationToken cancellationToken)
    {
        var tool = await repo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        repo.Delete(tool);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
