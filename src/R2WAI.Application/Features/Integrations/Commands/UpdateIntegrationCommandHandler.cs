namespace R2WAI.Application.Features.Integrations.Commands;

public class UpdateIntegrationCommandHandler(
    IRepository<ToolDefinition> repo,
    IUnitOfWork unitOfWork,
    ILogger<UpdateIntegrationCommandHandler> logger) : IRequestHandler<UpdateIntegrationCommand, Guid>
{
    public async Task<Guid> Handle(UpdateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var tool = await repo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        if (!Enum.TryParse<ToolType>(command.Type, true, out var toolType))
            throw new ValidationException("Type", $"Invalid integration type: {command.Type}");

        tool.Update(command.Name, command.Description, toolType, command.EndpointUrl, command.Configuration);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated integration {Name}", tool.Name);
        return tool.Id;
    }
}
