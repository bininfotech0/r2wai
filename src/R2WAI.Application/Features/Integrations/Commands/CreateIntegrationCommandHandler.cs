using R2WAI.Application.Common.Security;

namespace R2WAI.Application.Features.Integrations.Commands;

public class CreateIntegrationCommandHandler(
    IRepository<ToolDefinition> repo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    ILogger<CreateIntegrationCommandHandler> logger) : IRequestHandler<CreateIntegrationCommand, Guid>
{
    public async Task<Guid> Handle(CreateIntegrationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        if (!Enum.TryParse<ToolType>(command.Type, true, out var toolType))
            throw new ValidationException("Type", $"Invalid integration type: {command.Type}");

        // Token/ApiKey/Password inside Configuration are encrypted at rest here, never stored as the
        // plaintext the client submitted — see IntegrationCredentialCodec's doc comment.
        var configuration = IntegrationCredentialCodec.EncryptSecrets(command.Configuration, encryptionService);

        var tool = new ToolDefinition(
            Guid.NewGuid(), tenantId, command.Name, toolType,
            command.Description, command.EndpointUrl, configuration);

        if (!string.IsNullOrWhiteSpace(command.HttpMethod) || !string.IsNullOrWhiteSpace(command.EndpointPath))
            tool.LinkApi(null, command.HttpMethod, command.EndpointPath);

        await repo.AddAsync(tool, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created integration {Name} of type {Type}", tool.Name, toolType);
        return tool.Id;
    }
}
