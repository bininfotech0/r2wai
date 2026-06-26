using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record CreateModelCommand : IRequest<ModelConfigDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public bool IsDefault { get; init; }
}

public class CreateModelCommandValidator : AbstractValidator<CreateModelCommand>
{
    public CreateModelCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Provider).NotEmpty().MaximumLength(100);
        RuleFor(v => v.ModelId).NotEmpty().MaximumLength(200);
    }
}

public class CreateModelCommandHandler(
    IRepository<ModelConfiguration> modelRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    IMapper mapper) : IRequestHandler<CreateModelCommand, ModelConfigDto>
{
    public async Task<ModelConfigDto> Handle(CreateModelCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var model = new ModelConfiguration(
            Guid.NewGuid(), tenantId, command.Name, command.Provider,
            command.ModelId, endpoint: command.Endpoint);

        model.UpdateDetails(command.Name, command.Provider, command.ModelId,
            command.MaxTokens, command.Temperature, command.TopP, command.Endpoint);

        if (!string.IsNullOrWhiteSpace(command.ApiKey))
            model.SetApiKey(encryptionService.Encrypt(command.ApiKey));

        if (command.IsDefault)
            model.SetDefault(true);

        await modelRepo.AddAsync(model, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ModelConfigDto>(model);
    }
}
