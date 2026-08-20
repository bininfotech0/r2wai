using FluentValidation;

namespace R2WAI.Application.Features.Members.Commands;

public record ConvertPointsCommand : IRequest<MemberWalletDto>
{
    public int Points { get; init; }
}

public class ConvertPointsCommandValidator : AbstractValidator<ConvertPointsCommand>
{
    public ConvertPointsCommandValidator()
    {
        RuleFor(v => v.Points).GreaterThan(0);
    }
}

public class ConvertPointsCommandHandler(
    IRepository<MemberWallet> walletRepo,
    IRepository<PointsTransaction> transactionRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<ConvertPointsCommand, MemberWalletDto>
{
    // 1 point = ₹1. Purely an internal ledger conversion — see the compliance note on
    // MemberWallet: no real money moves as part of this operation.
    private const decimal RupeesPerPoint = 1m;

    public async Task<MemberWalletDto> Handle(ConvertPointsCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var wallet = await walletRepo.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(MemberWallet), userId);

        var amount = command.Points * RupeesPerPoint;
        wallet.ConvertPointsToWallet(command.Points, amount);

        await transactionRepo.AddAsync(new PointsTransaction(
            Guid.NewGuid(), tenantId, userId, -command.Points, PointsTransactionReason.ConvertedToWallet,
            $"Converted {command.Points} points to ₹{amount:0.##} wallet balance."), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<MemberWalletDto>(wallet);
    }
}
