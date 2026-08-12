using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class GlobalPolicyConfiguration : IEntityTypeConfiguration<GlobalPolicy>
{
    public void Configure(EntityTypeBuilder<GlobalPolicy> builder)
    {
        builder.ToTable("GlobalPolicies");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Content)
            .HasColumnType("text");

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.ModifiedAt);

        builder.HasOne(p => p.Tenant)
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.TenantId, p.Type }).IsUnique();
    }
}
