using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record UpdateModelCommand : IRequest<ModelConfigDto>, IAuthorizedRequest
{
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
    public Guid Id { get; init; }
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

public class UpdateModelCommandValidator : AbstractValidator<UpdateModelCommand>
{
    public UpdateModelCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Provider).NotEmpty().MaximumLength(100);
        RuleFor(v => v.ModelId).NotEmpty().MaximumLength(200);
    }
}

public class UpdateModelCommandHandler(
    IRepository<ModelConfiguration> modelRepo,
    IUnitOfWork unitOfWork,
    IEncryptionService encryptionService,
    IMapper mapper) : IRequestHandler<UpdateModelCommand, ModelConfigDto>
{
    public async Task<ModelConfigDto> Handle(UpdateModelCommand command, CancellationToken cancellationToken)
    {
        var model = await modelRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ModelConfiguration), command.Id);

        model.UpdateDetails(command.Name, command.Provider, command.ModelId,
            command.MaxTokens, command.Temperature, command.TopP, command.Endpoint);

        if (!string.IsNullOrWhiteSpace(command.ApiKey))
            model.SetApiKey(encryptionService.Encrypt(command.ApiKey));

        model.SetDefault(command.IsDefault);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ModelConfigDto>(model);
    }
}
