using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ApprovalNotificationDispatchConfiguration : IEntityTypeConfiguration<ApprovalNotificationDispatch>
{
    public void Configure(EntityTypeBuilder<ApprovalNotificationDispatch> builder)
    {
        builder.ToTable("ApprovalNotificationDispatches");

        builder.HasKey(d => d.Id);

        // The actual dedup: NotifyApproversJobHandler checks this before sending, and a retry that
        // re-enters the same (request, level) can never insert a second row for the same approver.
        builder.HasIndex(d => new { d.ApprovalRequestId, d.EscalationLevel, d.ApproverId }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
