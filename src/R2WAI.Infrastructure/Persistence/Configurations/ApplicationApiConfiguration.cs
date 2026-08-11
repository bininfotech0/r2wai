using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ApplicationApiConfiguration : IEntityTypeConfiguration<ApplicationApi>
{
    public void Configure(EntityTypeBuilder<ApplicationApi> builder)
    {
        builder.ToTable("ApplicationApis");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.BaseUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.AuthScheme)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ApiAuthScheme.None);

        builder.Property(a => a.CredentialRef)
            .HasMaxLength(200);

        builder.Property(a => a.OpenApiSource)
            .HasMaxLength(1000);

        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt);

        builder.HasOne(a => a.Application)
            .WithMany()
            .HasForeignKey(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.ApplicationId, a.Name })
            .IsUnique();
    }
}
