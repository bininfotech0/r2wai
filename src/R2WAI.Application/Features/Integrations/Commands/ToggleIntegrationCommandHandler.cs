namespace R2WAI.Application.Features.Integrations.Commands;

public class ToggleIntegrationCommandHandler(
    IRepository<ToolDefinition> repo,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleIntegrationCommand, bool>
{
    public async Task<bool> Handle(ToggleIntegrationCommand command, CancellationToken cancellationToken)
    {
        var tool = await repo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        if (tool.IsActive)
            tool.Deactivate();
        else
            tool.Activate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return tool.IsActive;
    }
}
