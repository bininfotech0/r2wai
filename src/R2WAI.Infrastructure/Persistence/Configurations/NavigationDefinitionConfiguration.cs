using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class NavigationDefinitionConfiguration : IEntityTypeConfiguration<NavigationDefinition>
{
    public void Configure(EntityTypeBuilder<NavigationDefinition> builder)
    {
        builder.ToTable("NavigationDefinitions");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Label)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Icon)
            .HasMaxLength(200);

        builder.Property(n => n.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.RequiredRole)
            .HasMaxLength(50);

        builder.Property(n => n.IsExternal)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(n => n.Order)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.ModifiedAt);

        builder.HasOne(n => n.Tenant)
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Application)
            .WithMany()
            .HasForeignKey(n => n.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.ApplicationId, n.Order });
    }
}
