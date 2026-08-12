using FluentValidation;

namespace R2WAI.Application.Features.Navigation.Commands;

public record UpdateNavigationItemCommand : IRequest<NavigationItemDto>
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? RequiredRole { get; init; }
    public bool IsExternal { get; init; }
}

public class UpdateNavigationItemCommandValidator : AbstractValidator<UpdateNavigationItemCommand>
{
    public UpdateNavigationItemCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Label).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Path).NotEmpty().MaximumLength(500);
    }
}

public class UpdateNavigationItemCommandHandler(
    IRepository<NavigationDefinition> navRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateNavigationItemCommand, NavigationItemDto>
{
    public async Task<NavigationItemDto> Handle(UpdateNavigationItemCommand command, CancellationToken cancellationToken)
    {
        var item = await navRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(NavigationDefinition), command.Id);

        item.UpdateDetails(command.Label, command.Icon, command.Path, command.RequiredRole, command.IsExternal);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<NavigationItemDto>(item);
    }
}
