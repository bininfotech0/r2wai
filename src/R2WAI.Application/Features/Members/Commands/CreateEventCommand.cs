using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record CreateEventCommand : IRequest<MemberEventDto>, IAuthorizedRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PointsValue { get; init; }
    public DateTime EventDate { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.PointsValue).GreaterThan(0);
    }
}

public class CreateEventCommandHandler(
    IRepository<MemberEvent> eventRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateEventCommand, MemberEventDto>
{
    public async Task<MemberEventDto> Handle(CreateEventCommand command, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var memberEvent = new MemberEvent(Guid.NewGuid(), tenantId, command.Name.Trim(),
            command.PointsValue, command.EventDate, command.Description?.Trim());

        await eventRepo.AddAsync(memberEvent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<MemberEventDto>(memberEvent);
    }
}
