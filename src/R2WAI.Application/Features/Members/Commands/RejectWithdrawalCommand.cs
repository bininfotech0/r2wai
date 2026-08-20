using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record RejectWithdrawalCommand : IRequest<WithdrawalRequestDto>, IAuthorizedRequest
{
    public Guid Id { get; init; }
    public string? AdminNotes { get; init; }
    public string[] RequiredRoles => ["Admin", "SystemAdmin"];
}

public class RejectWithdrawalCommandValidator : AbstractValidator<RejectWithdrawalCommand>
{
    public RejectWithdrawalCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.AdminNotes).MaximumLength(1000);
    }
}

public class RejectWithdrawalCommandHandler(
    IRepository<WithdrawalRequest> requestRepo,
    IRepository<MemberWallet> walletRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<RejectWithdrawalCommand, WithdrawalRequestDto>
{
    public async Task<WithdrawalRequestDto> Handle(RejectWithdrawalCommand command, CancellationToken cancellationToken)
    {
        var reviewerId = currentUser.UserId ?? throw new UnauthorizedException();

        var request = await requestRepo.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WithdrawalRequest), command.Id);

        var wallet = await walletRepo.FirstOrDefaultAsync(w => w.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberWallet), request.UserId);

        // Reject() throws if the request isn't Pending, so the hold below only ever fires once
        // for a given request — this is the one place a bug would silently duplicate ledger value.
        request.Reject(reviewerId, command.AdminNotes);
        wallet.ReleaseHold(request.AmountRequested);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<WithdrawalRequestDto>(request);
    }
}
