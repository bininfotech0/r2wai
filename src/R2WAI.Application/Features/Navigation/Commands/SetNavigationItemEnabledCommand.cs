namespace R2WAI.Application.Features.Navigation.Commands;

public record SetNavigationItemEnabledCommand : IRequest<NavigationItemDto>
{
    public Guid Id { get; init; }
    public bool IsEnabled { get; init; }
}

public class SetNavigationItemEnabledCommandHandler(
    IRepository<NavigationDefinition> navRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<SetNavigationItemEnabledCommand, NavigationItemDto>
{
    public async Task<NavigationItemDto> Handle(SetNavigationItemEnabledCommand command, CancellationToken cancellationToken)
    {
        var item = await navRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NavigationDefinition), command.Id);

        if (command.IsEnabled) item.Enable();
        else item.Disable();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<NavigationItemDto>(item);
    }
}
