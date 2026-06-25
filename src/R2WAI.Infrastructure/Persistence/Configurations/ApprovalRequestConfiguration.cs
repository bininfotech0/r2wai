using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests");

        builder.HasKey(ar => ar.Id);

        builder.Property(ar => ar.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(ar => ar.Comments)
            .HasMaxLength(2000);

        builder.Property(ar => ar.Data)
            .HasColumnType("jsonb");

        builder.Property(ar => ar.ApproverRole)
            .HasMaxLength(100);

        builder.Property(ar => ar.RequestedAt).IsRequired();
        builder.Property(ar => ar.RespondedAt);
        builder.Property(ar => ar.DueAt);
        builder.Property(ar => ar.EscalationLevel).HasDefaultValue(0);

        builder.HasOne(ar => ar.Tenant)
            .WithMany()
            .HasForeignKey(ar => ar.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Workflow)
            .WithMany()
            .HasForeignKey(ar => ar.WorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.WorkflowInstance)
            .WithMany()
            .HasForeignKey(ar => ar.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Requester)
            .WithMany()
            .HasForeignKey(ar => ar.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ar => new { ar.TenantId, ar.Status });
        builder.HasIndex(ar => new { ar.TenantId, ar.ApproverId });
        builder.HasIndex(ar => ar.DueAt).HasFilter("\"DueAt\" IS NOT NULL AND \"Status\" = 'Pending'");
    }
}
