using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record UpdateEventCommand : IRequest<MemberEventDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PointsValue { get; init; }
    public DateTime EventDate { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Description).MaximumLength(2000);
        RuleFor(v => v.PointsValue).GreaterThan(0);
    }
}

public class UpdateEventCommandHandler(
    IRepository<MemberEvent> eventRepo,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IRequestHandler<UpdateEventCommand, MemberEventDto>
{
    public async Task<MemberEventDto> Handle(UpdateEventCommand command, CancellationToken cancellationToken)
    {
        var memberEvent = await eventRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberEvent), command.Id);

        memberEvent.UpdateDetails(command.Name.Trim(), command.PointsValue, command.EventDate, command.Description?.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<MemberEventDto>(memberEvent);
    }
}
