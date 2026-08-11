namespace R2WAI.Application.Features.Applications.Commands;

public record DeleteDepartmentCommand : IRequest<Unit>
{
    public Guid Id { get; init; }
}

public class DeleteDepartmentCommandHandler(
    IRepository<Department> departmentRepo,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteDepartmentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDepartmentCommand command, CancellationToken cancellationToken)
    {
        var department = await departmentRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), command.Id);

        departmentRepo.Delete(department);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
