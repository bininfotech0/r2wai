namespace R2WAI.Application.Features.Applications.Queries;

public record GetDepartmentByIdQuery : IRequest<DepartmentDto>
{
    public Guid Id { get; init; }
}

public class GetDepartmentByIdQueryHandler(
    IRepository<Department> departmentRepo,
    IMapper mapper) : IRequestHandler<GetDepartmentByIdQuery, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(GetDepartmentByIdQuery query, CancellationToken cancellationToken)
    {
        var department = await departmentRepo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), query.Id);

        if (department.IsDeleted)
            throw new NotFoundException(nameof(Department), query.Id);

        return mapper.Map<DepartmentDto>(department);
    }
}
