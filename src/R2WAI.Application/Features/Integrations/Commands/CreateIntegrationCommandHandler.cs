namespace R2WAI.Application.Features.Integrations.Commands;

public class CreateIntegrationCommandHandler(
    IRepository<ToolDefinition> repo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<CreateIntegrationCommandHandler> logger) : IRequestHandler<CreateIntegrationCommand, Guid>
{
    public async Task<Guid> Handle(CreateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        if (!Enum.TryParse<ToolType>(command.Type, true, out var toolType))
            throw new ValidationException("Type", $"Invalid integration type: {command.Type}");

        var tool = new ToolDefinition(
            Guid.NewGuid(), tenantId, command.Name, toolType,
            command.Description, command.EndpointUrl, command.Configuration);

        await repo.AddAsync(tool, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created integration {Name} of type {Type}", tool.Name, toolType);
        return tool.Id;
    }
}
