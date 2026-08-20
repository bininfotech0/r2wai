using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record RequestPlanUpgradeCommand : IRequest<PlanUpgradeRequestDto>
{
    public string RequestedTier { get; init; } = string.Empty;
    public string? PaymentReference { get; init; }
}

public class RequestPlanUpgradeCommandValidator : AbstractValidator<RequestPlanUpgradeCommand>
{
    public RequestPlanUpgradeCommandValidator()
    {
        RuleFor(v => v.RequestedTier)
            .NotEmpty()
            .Must(v => Enum.TryParse<PlanTier>(v, true, out _))
            .WithMessage("RequestedTier must be one of: Free, Premium.");
        RuleFor(v => v.PaymentReference).MaximumLength(500);
    }
}

public class RequestPlanUpgradeCommandHandler(
    IRepository<PlanUpgradeRequest> requestRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RequestPlanUpgradeCommand, PlanUpgradeRequestDto>
{
    public async Task<PlanUpgradeRequestDto> Handle(RequestPlanUpgradeCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var tier = Enum.Parse<PlanTier>(command.RequestedTier, true);
        var request = new PlanUpgradeRequest(Guid.NewGuid(), tenantId, userId, tier,
            string.IsNullOrWhiteSpace(command.PaymentReference) ? null : command.PaymentReference.Trim());

        await requestRepo.AddAsync(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<PlanUpgradeRequestDto>(request);
    }
}
