using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ModelConfigurationConfiguration : IEntityTypeConfiguration<ModelConfiguration>
{
    public void Configure(EntityTypeBuilder<ModelConfiguration> builder)
    {
        builder.ToTable("ModelConfigurations");

        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mc => mc.Provider)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(mc => mc.ModelId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mc => mc.ApiKeyEncrypted)
            .HasMaxLength(1000);

        builder.Property(mc => mc.Endpoint)
            .HasMaxLength(500);

        builder.Property(mc => mc.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(mc => mc.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(mc => mc.CreatedAt).IsRequired();
        builder.Property(mc => mc.ModifiedAt);

        builder.HasOne(mc => mc.Tenant)
            .WithMany()
            .HasForeignKey(mc => mc.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(mc => new { mc.TenantId, mc.IsDefault });
    }
}
