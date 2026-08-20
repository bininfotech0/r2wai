using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record RequestWithdrawalCommand : IRequest<WithdrawalRequestDto>
{
    public decimal AmountRequested { get; init; }
    public string PayoutMethod { get; init; } = string.Empty;
}

public class RequestWithdrawalCommandValidator : AbstractValidator<RequestWithdrawalCommand>
{
    public RequestWithdrawalCommandValidator()
    {
        RuleFor(v => v.AmountRequested).GreaterThan(0);
        RuleFor(v => v.PayoutMethod).NotEmpty().MaximumLength(500);
    }
}

public class RequestWithdrawalCommandHandler(
    IRepository<MemberWallet> walletRepo,
    IRepository<WithdrawalRequest> requestRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RequestWithdrawalCommand, WithdrawalRequestDto>
{
    public async Task<WithdrawalRequestDto> Handle(RequestWithdrawalCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var wallet = await walletRepo.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberWallet), userId);

        // Reserves the funds immediately so the member can't submit two requests against the
        // same balance; released back on Reject, kept held through Approve/Complete.
        wallet.HoldForWithdrawal(command.AmountRequested);

        var request = new WithdrawalRequest(Guid.NewGuid(), tenantId, userId, command.AmountRequested, command.PayoutMethod.Trim());
        await requestRepo.AddAsync(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<WithdrawalRequestDto>(request);
        return dto;
    }
}
