using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class AssistantPromptHistoryConfiguration : IEntityTypeConfiguration<AssistantPromptHistory>
{
    public void Configure(EntityTypeBuilder<AssistantPromptHistory> builder)
    {
        builder.ToTable("AssistantPromptHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(h => h.Version)
            .IsRequired();

        builder.Property(h => h.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(h => h.CreatedAt).IsRequired();
        builder.Property(h => h.ModifiedAt);

        builder.HasOne(h => h.AssistantDefinition)
            .WithMany()
            .HasForeignKey(h => h.AssistantDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.AssistantDefinitionId, h.Version })
            .IsUnique();

        // Mirrors PromptTemplateConfiguration's active-lookup index shape.
        builder.HasIndex(h => new { h.AssistantDefinitionId, h.IsActive });
    }
}
