using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record ApprovePlanUpgradeCommand : IRequest<PlanUpgradeRequestDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class ApprovePlanUpgradeCommandValidator : AbstractValidator<ApprovePlanUpgradeCommand>
{
    public ApprovePlanUpgradeCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
    }
}

public class ApprovePlanUpgradeCommandHandler(
    IRepository<PlanUpgradeRequest> requestRepo,
    IRepository<MemberWallet> walletRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<ApprovePlanUpgradeCommand, PlanUpgradeRequestDto>
{
    public async Task<PlanUpgradeRequestDto> Handle(ApprovePlanUpgradeCommand command, CancellationToken cancellationToken)
    {
        var reviewerId = currentUser.UserId ?? throw new UnauthorizedException();

        var request = await requestRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PlanUpgradeRequest), command.Id);

        var wallet = await walletRepo.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberWallet), request.UserId);

        request.Approve(reviewerId);
        wallet.UpgradePlan(request.RequestedTier);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<PlanUpgradeRequestDto>(request);
    }
}
