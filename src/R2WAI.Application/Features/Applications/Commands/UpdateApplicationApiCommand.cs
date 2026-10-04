using FluentValidation;
using R2WAI.Application.Common.Validation;

namespace R2WAI.Application.Features.Applications.Commands;

public record UpdateApplicationApiCommand : IRequest<ApplicationApiDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public ApiAuthScheme AuthScheme { get; init; } = ApiAuthScheme.None;
    public string? CredentialRef { get; init; }

    // Left null/empty to keep the existing stored secret unchanged — the edit dialog never
    // round-trips the decrypted value, so "no change" must not mean "clear the credential".
    public string? CredentialSecret { get; init; }
    public string? CredentialHeaderName { get; init; }
    public string? OpenApiSource { get; init; }
}

public class UpdateApplicationApiCommandValidator : AbstractValidator<UpdateApplicationApiCommand>
{
    public UpdateApplicationApiCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.BaseUrl).NotEmpty().MaximumLength(500).MustBeValidHttpUrl();
        RuleFor(v => v.CredentialRef).MaximumLength(200);
        RuleFor(v => v.CredentialHeaderName).MaximumLength(200);
        RuleFor(v => v.OpenApiSource).MaximumLength(1000);
    }
}

public class UpdateApplicationApiCommandHandler(
    IRepository<ApplicationApi> apiRepo,
    IUnitOfWork unitOfWork,
    IEncryptionService encryptionService,
    IMapper mapper) : IRequestHandler<UpdateApplicationApiCommand, ApplicationApiDto>
{
    public async Task<ApplicationApiDto> Handle(UpdateApplicationApiCommand command, CancellationToken cancellationToken)
    {
        var api = await apiRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationApi), command.Id);

        api.UpdateDetails(command.Name, command.BaseUrl, command.AuthScheme, command.CredentialRef, command.OpenApiSource);

        if (!string.IsNullOrEmpty(command.CredentialSecret))
            api.SetCredential(encryptionService.Encrypt(command.CredentialSecret), command.CredentialHeaderName);
        else if (command.CredentialHeaderName != api.CredentialHeaderName)
            api.SetCredential(api.CredentialSecretEncrypted, command.CredentialHeaderName);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationApiDto>(api);
    }
}
