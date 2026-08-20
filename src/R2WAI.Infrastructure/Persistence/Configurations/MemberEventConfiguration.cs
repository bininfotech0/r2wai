using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class MemberEventConfiguration : IEntityTypeConfiguration<MemberEvent>
{
    public void Configure(EntityTypeBuilder<MemberEvent> builder)
    {
        builder.ToTable("MemberEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.PointsValue).IsRequired();
        builder.Property(e => e.EventDate).IsRequired();
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ModifiedAt);

        builder.HasIndex(e => new { e.TenantId, e.IsActive });
    }
}
