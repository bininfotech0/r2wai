namespace R2WAI.Application.Features.Applications.Commands;

public record ChangeApplicationStatusCommand : IRequest<ApplicationDto>
{
    public Guid Id { get; init; }
    public ApplicationAction Action { get; init; }
}

public class ChangeApplicationStatusCommandHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<ChangeApplicationStatusCommand, ApplicationDto>
{
    public async Task<ApplicationDto> Handle(ChangeApplicationStatusCommand command, CancellationToken cancellationToken)
    {
        var application = await applicationRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.Id);

        switch (command.Action)
        {
            case ApplicationAction.StartDiscovery:
                application.StartDiscovery();
                break;
            case ApplicationAction.MarkConfiguring:
                application.MarkConfiguring();
                break;
            case ApplicationAction.MarkTesting:
                application.MarkTesting();
                break;
            case ApplicationAction.Publish:
                application.Publish();
                break;
            case ApplicationAction.Disable:
                application.Disable();
                break;
            case ApplicationAction.Enable:
                application.Enable();
                break;
            case ApplicationAction.Archive:
                application.Archive();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command.Action), command.Action, null);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ApplicationDto>(application);
    }
}
