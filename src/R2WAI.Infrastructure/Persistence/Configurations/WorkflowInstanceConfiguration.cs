using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.Data)
            .HasColumnType("jsonb");

        builder.Property(i => i.CurrentStep)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.ModifiedAt);

        builder.HasOne(i => i.Workflow)
            .WithMany(w => w.Instances)
            .HasForeignKey(i => i.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Initiator)
            .WithMany()
            .HasForeignKey(i => i.InitiatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.WorkflowId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.Status });
    }
}
