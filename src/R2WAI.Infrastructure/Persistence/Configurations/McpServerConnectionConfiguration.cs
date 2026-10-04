using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class McpServerConnectionConfiguration : IEntityTypeConfiguration<McpServerConnection>
{
    public void Configure(EntityTypeBuilder<McpServerConnection> builder)
    {
        builder.ToTable("McpServerConnections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.EndpointUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.AuthHeaderName)
            .HasMaxLength(200);

        builder.Property(c => c.CredentialEncrypted)
            .HasMaxLength(2000);

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.LastTestStatus)
            .HasMaxLength(20);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ModifiedAt);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TenantId, c.Name });
    }
}
