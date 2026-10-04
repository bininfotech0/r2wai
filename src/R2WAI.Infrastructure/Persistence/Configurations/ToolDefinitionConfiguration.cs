using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ToolDefinitionConfiguration : IEntityTypeConfiguration<ToolDefinition>
{
    public void Configure(EntityTypeBuilder<ToolDefinition> builder)
    {
        builder.ToTable("ToolDefinitions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.ToolType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.EndpointUrl)
            .HasMaxLength(2000);

        builder.Property(t => t.HttpMethod)
            .HasMaxLength(10);

        builder.Property(t => t.EndpointPath)
            .HasMaxLength(500);

        builder.Property(t => t.Configuration)
            .HasColumnType("jsonb");

        builder.Property(t => t.McpToolName)
            .HasMaxLength(200);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.RiskLevel)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Low");

        builder.Property(t => t.RequiredRole)
            .HasMaxLength(50);

        builder.Property(t => t.ConfirmationRequired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.ApprovalRequired)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(t => t.AuditRequired)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.LastTestStatus)
            .HasMaxLength(20);

        builder.Property(t => t.LastTestedAt);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Application)
            .WithMany()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.ApplicationApi)
            .WithMany()
            .HasForeignKey(t => t.ApplicationApiId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.McpServerConnection)
            .WithMany()
            .HasForeignKey(t => t.McpServerConnectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.TenantId, t.Name });
        builder.HasIndex(t => new { t.TenantId, t.IsActive });
        builder.HasIndex(t => t.ApplicationId);
    }
}
