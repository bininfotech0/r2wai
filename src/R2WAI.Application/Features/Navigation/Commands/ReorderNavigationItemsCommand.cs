using FluentValidation;

namespace R2WAI.Application.Features.Navigation.Commands;

public record ReorderNavigationItemsCommand : IRequest<Unit>
{
    public Guid ApplicationId { get; init; }
    public List<Guid> OrderedIds { get; init; } = new();
}

public class ReorderNavigationItemsCommandValidator : AbstractValidator<ReorderNavigationItemsCommand>
{
    public ReorderNavigationItemsCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v.OrderedIds).NotEmpty();
    }
}

public class ReorderNavigationItemsCommandHandler(
    IRepository<NavigationDefinition> navRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<ReorderNavigationItemsCommand, Unit>
{
    public async Task<Unit> Handle(ReorderNavigationItemsCommand command, CancellationToken cancellationToken)
    {
        var items = await navRepo.FindAsync(
            n => n.ApplicationId == command.ApplicationId && !n.IsDeleted, cancellationToken);
        var byId = items.ToDictionary(n => n.Id);

        for (var i = 0; i < command.OrderedIds.Count; i++)
        {
            if (byId.TryGetValue(command.OrderedIds[i], out var item))
                item.SetOrder(i);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
