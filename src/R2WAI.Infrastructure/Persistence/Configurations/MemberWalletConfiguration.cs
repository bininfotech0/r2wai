using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class MemberWalletConfiguration : IEntityTypeConfiguration<MemberWallet>
{
    public void Configure(EntityTypeBuilder<MemberWallet> builder)
    {
        builder.ToTable("MemberWallets");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.PointsBalance).IsRequired();
        builder.Property(w => w.WalletBalanceInRupees).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(w => w.ReferralCode).IsRequired().HasMaxLength(20);
        builder.Property(w => w.PlanTier).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.ModifiedAt);

        builder.HasOne(w => w.User)
            .WithOne(u => u.MemberWallet)
            .HasForeignKey<MemberWallet>(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => w.UserId).IsUnique();
        builder.HasIndex(w => w.ReferralCode).IsUnique();
        builder.HasIndex(w => new { w.TenantId });
    }
}
