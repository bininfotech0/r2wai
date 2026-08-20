using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using R2WAI.Application.Features.Members;

namespace R2WAI.Application.Features.Auth.Commands;

public record RegisterMemberCommand : IRequest<MemberDto>
{
    public string FullName { get; init; } = string.Empty;
    public string AadhaarNumber { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? ReferralCode { get; init; }
}

public class RegisterMemberCommandValidator : AbstractValidator<RegisterMemberCommand>
{
    public RegisterMemberCommandValidator()
    {
        RuleFor(v => v.FullName).NotEmpty().MaximumLength(200);

        // Structural validation only (Verhoeff checksum) — this does not verify the number
        // against UIDAI's registry, no such integration exists.
        RuleFor(v => v.AadhaarNumber)
            .NotEmpty()
            .Must(Domain.Common.AadhaarValidator.IsStructurallyValid)
            .WithMessage("Please enter a valid 12-digit Aadhaar number.");

        RuleFor(v => v.MobileNumber)
            .NotEmpty()
            .Matches(@"^(\+91)?[6-9]\d{9}$")
            .WithMessage("Please enter a valid Indian mobile number.");

        RuleFor(v => v.Password)
            .NotEmpty()
            .Must(p => Common.Security.PasswordPolicy.IsValid(p, out _))
            .WithMessage(_ => $"Password must be at least {Common.Security.PasswordPolicy.MinLength} characters and include upper, lower, digit, and special characters.");

        RuleFor(v => v.Email).EmailAddress().When(v => !string.IsNullOrWhiteSpace(v.Email));

        RuleFor(v => v.ReferralCode).MaximumLength(20);
    }
}

public class RegisterMemberCommandHandler(
    IRepository<User> userRepo,
    IRepository<Tenant> tenantRepo,
    IRepository<MemberWallet> walletRepo,
    IRepository<PointsTransaction> transactionRepo,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IEncryptionService encryptionService) : IRequestHandler<RegisterMemberCommand, MemberDto>
{
    // Awarded to both sides of a referral on successful signup — an internal points ledger entry
    // only, see MemberWallet's compliance note.
    private const int ReferralBonusPoints = 100;

    public async Task<MemberDto> Handle(RegisterMemberCommand command, CancellationToken cancellationToken)
    {
        var aadhaarDigits = command.AadhaarNumber.Trim();
        var aadhaarHash = HashAadhaar(aadhaarDigits);

        var existing = await userRepo.FirstOrDefaultAsync(u => u.AadhaarNumberHash == aadhaarHash, cancellationToken);
        if (existing is not null)
            throw new ValidationException(nameof(command.AadhaarNumber), "An account with this Aadhaar number already exists.");

        var tenant = await tenantRepo.FirstOrDefaultAsync(t => t.Slug == "default", cancellationToken)
            ?? (await tenantRepo.GetAllAsync(cancellationToken)).FirstOrDefault()
            ?? throw new InvalidOperationException("No tenant is configured for this deployment.");

        var mobile = command.MobileNumber.Trim();
        var email = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim();
        var nameParts = command.FullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : command.FullName.Trim();
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        var member = Domain.Entities.User.CreateMember(
            Guid.NewGuid(), tenant.Id,
            encryptionService.Encrypt(aadhaarDigits), aadhaarHash,
            mobile, firstName, lastName, email);

        member.SetPasswordHash(passwordHasher.Hash(command.Password));

        await userRepo.AddAsync(member, cancellationToken);

        MemberWallet? referrerWallet = null;
        if (!string.IsNullOrWhiteSpace(command.ReferralCode))
        {
            referrerWallet = await walletRepo.FirstOrDefaultAsync(
                w => w.ReferralCode == command.ReferralCode.Trim().ToUpperInvariant(), cancellationToken);
        }

        var referralCode = await GenerateUniqueReferralCode(cancellationToken);
        var wallet = new MemberWallet(Guid.NewGuid(), tenant.Id, member.Id, referralCode, referrerWallet?.UserId);
        await walletRepo.AddAsync(wallet, cancellationToken);

        if (referrerWallet is not null)
        {
            wallet.AddPoints(ReferralBonusPoints);
            referrerWallet.AddPoints(ReferralBonusPoints);

            await transactionRepo.AddAsync(new PointsTransaction(
                Guid.NewGuid(), tenant.Id, member.Id, ReferralBonusPoints,
                PointsTransactionReason.ReferralBonus, "Signed up using a referral code."), cancellationToken);
            await transactionRepo.AddAsync(new PointsTransaction(
                Guid.NewGuid(), tenant.Id, referrerWallet.UserId, ReferralBonusPoints,
                PointsTransactionReason.ReferralBonus, "Referred a new member."), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MemberDto
        {
            Id = member.Id,
            TenantId = member.TenantId,
            Email = member.Email,
            FirstName = member.FirstName,
            LastName = member.LastName,
            MobileNumber = mobile,
            ReferralCode = referralCode,
        };
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

    // Mirrors JwtService.HashRefreshToken's exact convention (plain SHA-256 -> Base64) so this
    // introduces no new key-management concept — the hash is deterministic purely for uniqueness
    // and login lookup, never used to reconstruct the original number.
    private static string HashAadhaar(string aadhaarNumber)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(aadhaarNumber));
        return Convert.ToBase64String(bytes);
    }
}
