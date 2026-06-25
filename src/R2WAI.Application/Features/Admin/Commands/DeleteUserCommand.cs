using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record DeleteUserCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class DeleteUserCommandHandler(
    IRepository<User> userRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), command.Id);

        user.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
