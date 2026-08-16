using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        builder.ToTable("AccessRequests");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FullName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Email).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Organization).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Department).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Reason).HasMaxLength(2000);
        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt);

        builder.HasIndex(a => a.Email);
        builder.HasIndex(a => a.Status);
    }
}
