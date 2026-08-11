using FluentValidation;

namespace R2WAI.Application.Features.Applications.Commands;

public record UpdateDepartmentCommand : IRequest<DepartmentDto>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? HeadUserId { get; init; }
}

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
    }
}

public class UpdateDepartmentCommandHandler(
    IRepository<Department> departmentRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var department = await departmentRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), command.Id);

        department.UpdateDetails(command.Name, command.Description);
        department.SetHead(command.HeadUserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<DepartmentDto>(department);
    }
}
