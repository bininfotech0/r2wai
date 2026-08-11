using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ConnectedApplicationConfiguration : IEntityTypeConfiguration<ConnectedApplication>
{
    public void Configure(EntityTypeBuilder<ConnectedApplication> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Description)
            .HasMaxLength(2000);

        builder.Property(a => a.BaseUrl)
            .HasMaxLength(500);

        builder.Property(a => a.Environment)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ApplicationEnvironment.Development);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ApplicationStatus.Draft);

        builder.Property(a => a.PublishedAt);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt);

        builder.HasOne(a => a.Tenant)
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Department)
            .WithMany(d => d.Applications)
            .HasForeignKey(a => a.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TenantId, a.DepartmentId, a.Code })
            .IsUnique();

        builder.HasIndex(a => new { a.TenantId, a.Status });
    }
}
