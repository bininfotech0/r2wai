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
        RuleFor(v => v.OpenApiSource).MaximumLength(1000);
    }
}

public class UpdateApplicationApiCommandHandler(
    IRepository<ApplicationApi> apiRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateApplicationApiCommand, ApplicationApiDto>
{
    public async Task<ApplicationApiDto> Handle(UpdateApplicationApiCommand command, CancellationToken cancellationToken)
    {
        var api = await apiRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApplicationApi), command.Id);

        api.UpdateDetails(command.Name, command.BaseUrl, command.AuthScheme, command.CredentialRef, command.OpenApiSource);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationApiDto>(api);
    }
}
