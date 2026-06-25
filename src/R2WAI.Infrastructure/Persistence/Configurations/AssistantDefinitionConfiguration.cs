using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class AssistantDefinitionConfiguration : IEntityTypeConfiguration<AssistantDefinition>
{
    public void Configure(EntityTypeBuilder<AssistantDefinition> builder)
    {
        builder.ToTable("AssistantDefinitions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Description)
            .HasMaxLength(2000);

        builder.Property(a => a.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.SystemPrompt)
            .HasColumnType("text");

        builder.Property(a => a.Tools)
            .HasColumnType("jsonb");

        builder.Property(a => a.Settings)
            .HasColumnType("jsonb");

        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt);

        builder.HasOne(a => a.Tenant)
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ModelConfiguration)
            .WithMany()
            .HasForeignKey(a => a.ModelConfigurationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.KnowledgeBase)
            .WithMany()
            .HasForeignKey(a => a.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => new { a.TenantId, a.Name });
        builder.HasIndex(a => new { a.TenantId, a.IsActive });
    }
}
