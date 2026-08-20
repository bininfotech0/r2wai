using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class PlanUpgradeRequestConfiguration : IEntityTypeConfiguration<PlanUpgradeRequest>
{
    public void Configure(EntityTypeBuilder<PlanUpgradeRequest> builder)
    {
        builder.ToTable("PlanUpgradeRequests");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.RequestedTier).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.PaymentReference).HasMaxLength(500);
        builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.ModifiedAt);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TenantId, p.Status });
    }
}
