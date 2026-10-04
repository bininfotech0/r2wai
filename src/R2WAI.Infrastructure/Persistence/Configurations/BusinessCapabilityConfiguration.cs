using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class BusinessCapabilityConfiguration : IEntityTypeConfiguration<BusinessCapability>
{
    public void Configure(EntityTypeBuilder<BusinessCapability> builder)
    {
        builder.ToTable("BusinessCapabilities");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Property(c => c.Icon)
            .HasMaxLength(100);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BusinessCapabilityStatus.Draft);

        builder.Property(c => c.ToolIds).HasColumnType("jsonb");
        builder.Property(c => c.KnowledgeBaseIds).HasColumnType("jsonb");
        builder.Property(c => c.WorkflowIds).HasColumnType("jsonb");
        builder.Property(c => c.ApplicationApiIds).HasColumnType("jsonb");

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ModifiedAt);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Assistant)
            .WithMany()
            .HasForeignKey(c => c.AssistantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.TenantId, c.AssistantId });
    }
}
