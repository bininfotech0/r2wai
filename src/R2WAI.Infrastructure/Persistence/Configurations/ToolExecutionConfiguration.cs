using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("ToolExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Plugin).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Function).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.DenialReason).HasMaxLength(500);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(200);
        builder.Property(e => e.Error).HasMaxLength(4000);

        // Activity listings (newest first per tenant), the resume lookup from an approval, and the
        // reserved Prepare-step dedup key (unique only once populated).
        builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
        builder.HasIndex(e => e.ApprovalRequestId);
        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ledger rows must outlive what they point at, so these are SetNull, not Cascade.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ToolDefinition>()
            .WithMany()
            .HasForeignKey(e => e.ToolDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApprovalRequest>()
            .WithMany()
            .HasForeignKey(e => e.ApprovalRequestId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
