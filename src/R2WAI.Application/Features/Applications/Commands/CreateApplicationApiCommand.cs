using FluentValidation;
using R2WAI.Application.Common.Validation;

namespace R2WAI.Application.Features.Applications.Commands;

public record CreateApplicationApiCommand : IRequest<ApplicationApiDto>
{
    public Guid ApplicationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public ApiAuthScheme AuthScheme { get; init; } = ApiAuthScheme.None;
    public string? CredentialRef { get; init; }

    // Plaintext on the wire (HTTPS + [Authorize]), never persisted as-is — the handler encrypts it
    // before it touches the database and it's never echoed back in ApplicationApiDto.
    public string? CredentialSecret { get; init; }
    public string? CredentialHeaderName { get; init; }
    public string? OpenApiSource { get; init; }
}

public class CreateApplicationApiCommandValidator : AbstractValidator<CreateApplicationApiCommand>
{
    public CreateApplicationApiCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.BaseUrl).NotEmpty().MaximumLength(500).MustBeValidHttpUrl();
        RuleFor(v => v.CredentialRef).MaximumLength(200);
        RuleFor(v => v.CredentialHeaderName).MaximumLength(200);
        RuleFor(v => v.CredentialHeaderName).NotEmpty()
            .When(v => v.AuthScheme == ApiAuthScheme.ApiKey && !string.IsNullOrEmpty(v.CredentialSecret))
            .WithMessage("A header name is required for ApiKey authentication.");
        RuleFor(v => v.OpenApiSource).MaximumLength(1000);
    }
}

public class CreateApplicationApiCommandHandler(
    IRepository<ApplicationApi> apiRepo,
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IEncryptionService encryptionService,
    IMapper mapper) : IRequestHandler<CreateApplicationApiCommand, ApplicationApiDto>
{
    public async Task<ApplicationApiDto> Handle(CreateApplicationApiCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var application = await applicationRepo.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.ApplicationId);

        var api = new ApplicationApi(
            Guid.NewGuid(), tenantId, application.Id, command.Name, command.BaseUrl,
            command.AuthScheme, command.CredentialRef, command.OpenApiSource);

        if (!string.IsNullOrEmpty(command.CredentialSecret))
            api.SetCredential(encryptionService.Encrypt(command.CredentialSecret), command.CredentialHeaderName);

        await apiRepo.AddAsync(api, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationApiDto>(api);
    }
}
