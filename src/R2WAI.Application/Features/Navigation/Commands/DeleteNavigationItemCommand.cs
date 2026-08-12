namespace R2WAI.Application.Features.Navigation.Commands;

public record DeleteNavigationItemCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteNavigationItemCommandHandler(
    IRepository<NavigationDefinition> navRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteNavigationItemCommand, Unit>
{
    public async Task<Unit> Handle(DeleteNavigationItemCommand command, CancellationToken cancellationToken)
    {
        var item = await navRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NavigationDefinition), command.Id);

        navRepo.Delete(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
