using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        builder.ToTable("ApprovalPolicies");

        builder.HasKey(ap => ap.Id);

        builder.Property(ap => ap.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ap => ap.Description)
            .HasMaxLength(2000);

        builder.Property(ap => ap.WorkflowType)
            .HasMaxLength(100);

        builder.Property(ap => ap.ApproverRoles)
            .HasColumnType("jsonb");

        builder.Property(ap => ap.EscalationRoles)
            .HasColumnType("jsonb");

        builder.Property(ap => ap.MinApprovers)
            .HasDefaultValue(1);

        builder.Property(ap => ap.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(ap => ap.Tenant)
            .WithMany()
            .HasForeignKey(ap => ap.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ap => new { ap.TenantId, ap.WorkflowType });
        builder.HasIndex(ap => new { ap.TenantId, ap.Name }).IsUnique();
    }
}
