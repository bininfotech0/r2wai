using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        builder.ToTable("PromptTemplates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.AssistantType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(t => t.Version)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // The active-lookup path filters on (TenantId, AssistantType, IsActive); Version is only
        // read for display/history, never queried on directly.
        builder.HasIndex(t => new { t.TenantId, t.AssistantType, t.IsActive });
    }
}
