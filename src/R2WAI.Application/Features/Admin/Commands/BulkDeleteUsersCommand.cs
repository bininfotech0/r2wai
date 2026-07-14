using FluentValidation;

namespace R2WAI.Application.Features.Admin.Commands;

public record BulkDeleteUsersCommand : IRequest<Unit>, IAuthorizedRequest
{
    public Guid[] Ids { get; init; } = [];
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class BulkDeleteUsersCommandValidator : AbstractValidator<BulkDeleteUsersCommand>
{
    public BulkDeleteUsersCommandValidator()
    {
        RuleFor(v => v.Ids).NotEmpty();
    }
}

public class BulkDeleteUsersCommandHandler(
    IRepository<User> userRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<BulkDeleteUsersCommand, Unit>
{
    public async Task<Unit> Handle(BulkDeleteUsersCommand command, CancellationToken cancellationToken)
    {
        foreach (var id in command.Ids)
        {
            var user = await userRepo.GetByIdAsync(id, cancellationToken);
            user?.SoftDelete();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
