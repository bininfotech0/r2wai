using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ConnectedApplicationConfiguration : IEntityTypeConfiguration<ConnectedApplication>
{
    public void Configure(EntityTypeBuilder<ConnectedApplication> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(a => a.Id);

        // Optimistic concurrency via Postgres's built-in xmin system column — a shadow property, not
        // a real new column, so no migration needed. Guards the status-transition race the Workspace
        // Boundary Audit flagged.
        builder.Property<uint>("xmin").IsRowVersion().HasColumnName("xmin");

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

        // Code is unique per tenant *within* a department. Because a connected system no longer
        // requires a department, this index alone leaves DepartmentId = NULL rows unconstrained:
        // Postgres treats NULLs as distinct in a unique index, so two department-less systems
        // sharing a Code would both be accepted. The filtered index below restores tenant-level
        // uniqueness for exactly those rows, without narrowing the existing constraint for
        // department-scoped ones.
        builder.HasIndex(a => new { a.TenantId, a.DepartmentId, a.Code })
            .IsUnique();

        builder.HasIndex(a => new { a.TenantId, a.Code })
            .IsUnique()
            .HasFilter("\"DepartmentId\" IS NULL")
            .HasDatabaseName("IX_Applications_TenantId_Code_NoDepartment");

        builder.HasIndex(a => new { a.TenantId, a.Status });
    }
}
