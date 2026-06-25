using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record DeleteRoleCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    public DeleteRoleCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteRoleCommandHandler(
    IRepository<Role> roleRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteRoleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await roleRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), command.Id);

        if (role.IsSystem)
            throw new ValidationException("Role", "System roles cannot be deleted.");

        role.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
