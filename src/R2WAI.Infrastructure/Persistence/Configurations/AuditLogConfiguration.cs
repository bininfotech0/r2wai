using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(al => al.Id);

        builder.Property(al => al.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(al => al.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(al => al.EntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(al => al.OldValues)
            .HasColumnType("jsonb");

        builder.Property(al => al.NewValues)
            .HasColumnType("jsonb");

        builder.Property(al => al.IpAddress)
            .HasMaxLength(50);

        builder.Property(al => al.UserAgent)
            .HasMaxLength(500);

        builder.Property(al => al.Metadata)
            .HasColumnType("jsonb");

        builder.Property(al => al.Timestamp)
            .IsRequired();

        builder.Property(al => al.CreatedAt).IsRequired();

        builder.HasOne(al => al.Tenant)
            .WithMany()
            .HasForeignKey(al => al.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(al => al.User)
            .WithMany()
            .HasForeignKey(al => al.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(al => new { al.TenantId, al.Timestamp });
        builder.HasIndex(al => new { al.TenantId, al.EntityType, al.EntityId });
    }
}
