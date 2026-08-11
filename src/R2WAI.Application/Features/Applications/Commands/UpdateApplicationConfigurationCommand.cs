using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record UpdateApplicationConfigurationCommand : IRequest<ApplicationConfigurationDto>
{
    public Guid ApplicationId { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 3;
    public double RagThreshold { get; init; } = 0.7;
    public string? ModelId { get; init; }
    public string? SystemPromptTemplate { get; init; }
}

public class UpdateApplicationConfigurationCommandValidator : AbstractValidator<UpdateApplicationConfigurationCommand>
{
    public UpdateApplicationConfigurationCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v.TimeoutSeconds).GreaterThan(0);
        RuleFor(v => v.MaxRetries).GreaterThanOrEqualTo(0);
        RuleFor(v => v.RagThreshold).InclusiveBetween(0, 1);
        RuleFor(v => v.ModelId).MaximumLength(200);
        RuleFor(v => v.SystemPromptTemplate).MaximumLength(4000);
    }
}

public class UpdateApplicationConfigurationCommandHandler(
    IRepository<ApplicationConfiguration> configRepo,
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<UpdateApplicationConfigurationCommand, ApplicationConfigurationDto>
{
    public async Task<ApplicationConfigurationDto> Handle(UpdateApplicationConfigurationCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var configuration = await configRepo.FirstOrDefaultAsync(c => c.ApplicationId == command.ApplicationId, cancellationToken);

        if (configuration is null)
        {
            var application = await applicationRepo.GetByIdAsync(command.ApplicationId, cancellationToken)
                ?? throw new NotFoundException(nameof(ConnectedApplication), command.ApplicationId);

            configuration = new ApplicationConfiguration(Guid.NewGuid(), tenantId, application.Id);
            await configRepo.AddAsync(configuration, cancellationToken);
        }

        configuration.UpdateSettings(
            command.TimeoutSeconds, command.MaxRetries, command.RagThreshold,
            command.ModelId, command.SystemPromptTemplate);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationConfigurationDto>(configuration);
    }
}
