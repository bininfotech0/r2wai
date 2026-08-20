using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record RejectPlanUpgradeCommand : IRequest<PlanUpgradeRequestDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class RejectPlanUpgradeCommandValidator : AbstractValidator<RejectPlanUpgradeCommand>
{
    public RejectPlanUpgradeCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class RejectPlanUpgradeCommandHandler(
    IRepository<PlanUpgradeRequest> requestRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RejectPlanUpgradeCommand, PlanUpgradeRequestDto>
{
    public async Task<PlanUpgradeRequestDto> Handle(RejectPlanUpgradeCommand command, CancellationToken cancellationToken)
    {
        var reviewerId = currentUser.UserId ?? throw new UnauthorizedException();

        var request = await requestRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PlanUpgradeRequest), command.Id);

        request.Reject(reviewerId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<PlanUpgradeRequestDto>(request);
    }
}
