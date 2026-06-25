using Microsoft.EntityFrameworkCore.Metadata.Builders;
using R2WAI.Domain.Enums;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ToolDefinitionConfiguration : IEntityTypeConfiguration<ToolDefinition>
{
    public void Configure(EntityTypeBuilder<ToolDefinition> builder)
    {
        builder.ToTable("ToolDefinitions");

        builder.HasKey(td => td.Id);

        builder.Property(td => td.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(td => td.Description)
            .HasMaxLength(2000);

        builder.Property(td => td.ToolType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(td => td.EndpointUrl)
            .HasMaxLength(2000);

        builder.Property(td => td.Configuration)
            .HasColumnType("jsonb");

        builder.Property(td => td.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(td => td.Tenant)
            .WithMany()
            .HasForeignKey(td => td.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(td => new { td.TenantId, td.ToolType });
        builder.HasIndex(td => new { td.TenantId, td.Name }).IsUnique();
    }
}
