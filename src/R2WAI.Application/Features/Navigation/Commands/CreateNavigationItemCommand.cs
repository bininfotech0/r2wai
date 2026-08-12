using FluentValidation;

namespace R2WAI.Application.Features.Navigation.Commands;

public record CreateNavigationItemCommand : IRequest<NavigationItemDto>
{
    public Guid ApplicationId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? RequiredRole { get; init; }
    public bool IsExternal { get; init; }
}

public class CreateNavigationItemCommandValidator : AbstractValidator<CreateNavigationItemCommand>
{
    public CreateNavigationItemCommandValidator()
    {
        RuleFor(v => v.ApplicationId).NotEmpty();
        RuleFor(v => v.Label).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Path).NotEmpty().MaximumLength(500);
    }
}

public class CreateNavigationItemCommandHandler(
    IRepository<NavigationDefinition> navRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateNavigationItemCommand, NavigationItemDto>
{
    public async Task<NavigationItemDto> Handle(CreateNavigationItemCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var existing = await navRepo.FindAsync(n => n.ApplicationId == command.ApplicationId && !n.IsDeleted, cancellationToken);
        var nextOrder = existing.Count == 0 ? 0 : existing.Max(n => n.Order) + 1;

        var item = new NavigationDefinition(Guid.NewGuid(), tenantId, command.ApplicationId, command.Label, command.Path, nextOrder);
        item.UpdateDetails(command.Label, command.Icon, command.Path, command.RequiredRole, command.IsExternal);

        await navRepo.AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<NavigationItemDto>(item);
    }
}
