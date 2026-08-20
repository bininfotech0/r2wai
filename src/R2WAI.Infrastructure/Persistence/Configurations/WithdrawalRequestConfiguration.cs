using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
    {
        builder.ToTable("WithdrawalRequests");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.AmountRequested).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(w => w.PayoutMethod).IsRequired().HasMaxLength(500);
        builder.Property(w => w.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(w => w.AdminNotes).HasMaxLength(1000);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.ModifiedAt);

        builder.HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.TenantId, w.Status });
    }
}
