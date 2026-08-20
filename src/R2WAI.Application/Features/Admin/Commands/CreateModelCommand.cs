using FluentValidation;
using R2WAI.Application.Common.Validation;

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
    public Guid? ApplicationId { get; init; }
    public Guid? DepartmentId { get; init; }
    public string DataClassification { get; init; } = "Internal";
}

public class CreateModelCommandValidator : AbstractValidator<CreateModelCommand>
{
    public CreateModelCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Provider).NotEmpty().MaximumLength(100);
        RuleFor(v => v.ModelId).NotEmpty().MaximumLength(200);
        // Endpoint is the actual base URL chat/embedding calls get sent to (see
        // SemanticKernelService) — previously entirely unchecked, not even length-limited.
        RuleFor(v => v.Endpoint).MaximumLength(500).MustBeValidHttpUrl();
        RuleFor(v => v.MaxTokens).GreaterThan(0).When(v => v.MaxTokens.HasValue);
        RuleFor(v => v.Temperature).InclusiveBetween(0, 2).When(v => v.Temperature.HasValue);
        RuleFor(v => v.TopP).InclusiveBetween(0, 1).When(v => v.TopP.HasValue);
        RuleFor(v => v.DataClassification).Must(v => Enum.TryParse<Domain.Enums.DataClassification>(v, true, out _))
            .WithMessage("DataClassification must be one of: Public, Internal, Confidential, Restricted.");
        RuleFor(v => v.Provider).Must((v, provider) => !ValidationExtensions.ViolatesDataClassificationBoundary(v.DataClassification, provider))
            .WithMessage("Confidential or Restricted data classification requires a local provider (Ollama) — external providers are not permitted for this sensitivity level.");
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

        var dataClassification = Enum.Parse<Domain.Enums.DataClassification>(command.DataClassification, true);
        model.UpdateDetails(command.Name, command.Provider, command.ModelId,
            command.MaxTokens, command.Temperature, command.TopP, command.Endpoint,
            command.ApplicationId, command.DepartmentId, dataClassification);

        if (!string.IsNullOrWhiteSpace(command.ApiKey))
            model.SetApiKey(encryptionService.Encrypt(command.ApiKey));

        if (command.IsDefault)
            model.SetDefault(true);

        await modelRepo.AddAsync(model, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ModelConfigDto>(model);
    }
}
