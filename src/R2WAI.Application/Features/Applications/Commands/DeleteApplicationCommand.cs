namespace R2WAI.Application.Features.Applications.Commands;

public record DeleteApplicationCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteApplicationCommandHandler(
    IRepository<ConnectedApplication> applicationRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteApplicationCommand, Unit>
{
    public async Task<Unit> Handle(DeleteApplicationCommand command, CancellationToken cancellationToken)
    {
        var application = await applicationRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ConnectedApplication), command.Id);

        applicationRepo.Delete(application);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
