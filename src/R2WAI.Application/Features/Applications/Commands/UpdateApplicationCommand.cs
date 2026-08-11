using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record UpdateApplicationCommand : IRequest<ApplicationDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? BaseUrl { get; init; }
    public ApplicationEnvironment Environment { get; init; }
    public Guid? ManagerUserId { get; init; }
}

public class UpdateApplicationCommandValidator : AbstractValidator<UpdateApplicationCommand>
{
    public UpdateApplicationCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.BaseUrl).MaximumLength(500);
    }
}

public class UpdateApplicationCommandHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateApplicationCommand, ApplicationDto>
{
    public async Task<ApplicationDto> Handle(UpdateApplicationCommand command, CancellationToken cancellationToken)
    {
        var application = await applicationRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.Id);

        application.UpdateDetails(command.Name, command.Description, command.BaseUrl, command.Environment);
        application.SetManager(command.ManagerUserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationDto>(application);
    }
}
