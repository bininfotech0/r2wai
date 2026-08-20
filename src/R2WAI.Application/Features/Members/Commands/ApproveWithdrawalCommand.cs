using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record ApproveWithdrawalCommand : IRequest<WithdrawalRequestDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string? AdminNotes { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class ApproveWithdrawalCommandValidator : AbstractValidator<ApproveWithdrawalCommand>
{
    public ApproveWithdrawalCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.AdminNotes).MaximumLength(1000);
    }
}

public class ApproveWithdrawalCommandHandler(
    IRepository<WithdrawalRequest> requestRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<ApproveWithdrawalCommand, WithdrawalRequestDto>
{
    public async Task<WithdrawalRequestDto> Handle(ApproveWithdrawalCommand command, CancellationToken cancellationToken)
    {
        var reviewerId = currentUser.UserId ?? throw new UnauthorizedException();

        var request = await requestRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WithdrawalRequest), command.Id);

        // Approving commits the admin to pay out off-system; the funds were already reserved out of
        // the wallet balance when the request was created (MemberWallet.HoldForWithdrawal), so no
        // further wallet mutation happens here.
        request.Approve(reviewerId, command.AdminNotes);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WithdrawalRequestDto>(request);
    }
}
