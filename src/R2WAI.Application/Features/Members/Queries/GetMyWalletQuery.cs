namespace R2WAI.Application.Features.Members.Queries;

public record GetMyWalletQuery : IRequest<MemberWalletDto>;

public class GetMyWalletQueryHandler(
    IRepository<MemberWallet> walletRepo,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<GetMyWalletQuery, MemberWalletDto>
{
    public async Task<MemberWalletDto> Handle(GetMyWalletQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedException();

        var wallet = await walletRepo.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

        // Self-heals accounts registered before this feature existed — they never had a wallet
        // created at signup, so the first time they visit the wallet page one is provisioned.
        if (wallet is null)
        {
            var referralCode = await GenerateUniqueReferralCode(cancellationToken);
            wallet = new MemberWallet(Guid.NewGuid(), tenantId, userId, referralCode);
            await walletRepo.AddAsync(wallet, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return mapper.Map<MemberWalletDto>(wallet);
    }

    private async Task<string> GenerateUniqueReferralCode(CancellationToken cancellationToken)
    {
        string code;
        do
        {
            code = ReferralCodeGenerator.Generate();
        } while (await walletRepo.FirstOrDefaultAsync(w => w.ReferralCode == code, cancellationToken) is not null);

        return code;
    }
}
