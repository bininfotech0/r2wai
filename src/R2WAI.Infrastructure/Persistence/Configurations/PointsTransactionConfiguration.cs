using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class PointsTransactionConfiguration : IEntityTypeConfiguration<PointsTransaction>
{
    public void Configure(EntityTypeBuilder<PointsTransaction> builder)
    {
        builder.ToTable("PointsTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Points).IsRequired();
        builder.Property(t => t.Reason).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.Description).HasMaxLength(500);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.RelatedEvent)
            .WithMany()
            .HasForeignKey(t => t.RelatedEventId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.TenantId, t.UserId });
    }
}
