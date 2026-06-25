using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Description)
            .HasMaxLength(2000);

        builder.Property(w => w.Type)
            .HasMaxLength(100);

        builder.Property(w => w.Trigger)
            .HasMaxLength(100);

        builder.Property(w => w.Steps)
            .HasColumnType("jsonb");

        builder.Property(w => w.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.ModifiedAt);

        builder.HasOne(w => w.Tenant)
            .WithMany()
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Instances)
            .WithOne(i => i.Workflow)
            .HasForeignKey(i => i.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.TenantId, w.Name });
        builder.HasIndex(w => new { w.TenantId, w.IsActive });
        builder.HasIndex(w => new { w.Trigger, w.IsActive });
    }
}
