using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WorkflowTemplateOverrideConfiguration : IEntityTypeConfiguration<WorkflowTemplateOverride>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplateOverride> builder)
    {
        builder.ToTable("WorkflowTemplateOverrides");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TemplateId).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Type).IsRequired().HasMaxLength(50);
        builder.Property(t => t.StepsJson).IsRequired().HasColumnType("text");

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // One override per tenant+template — PUT is an upsert against this pair.
        builder.HasIndex(t => new { t.TenantId, t.TemplateId }).IsUnique();
    }
}
