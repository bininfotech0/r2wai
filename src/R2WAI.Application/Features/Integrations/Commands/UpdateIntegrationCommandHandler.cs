using R2WAI.Application.Common.Security;

namespace R2WAI.Application.Features.Integrations.Commands;

public class UpdateIntegrationCommandHandler(
    IRepository<ToolDefinition> repo,
    IUnitOfWork unitOfWork,
    IEncryptionService encryptionService,
    ILogger<UpdateIntegrationCommandHandler> logger) : IRequestHandler<UpdateIntegrationCommand, Guid>
{
    public async Task<Guid> Handle(UpdateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var tool = await repo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ToolDefinition), command.Id);

        if (!Enum.TryParse<ToolType>(command.Type, true, out var toolType))
            throw new ValidationException("Type", $"Invalid integration type: {command.Type}");

        // The client never sees a saved secret back (GetIntegrations redacts it), so "blank" on this
        // request means "unchanged", not "clear it" — MergeAndEncrypt preserves the existing encrypted
        // value for any secret field the incoming payload left out.
        var configuration = IntegrationCredentialCodec.MergeAndEncrypt(tool.Configuration, command.Configuration, encryptionService);

        tool.Update(command.Name, command.Description, toolType, command.EndpointUrl, configuration);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated integration {Name}", tool.Name);
        return tool.Id;
    }
}
