using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ApplicationConfigurationConfiguration : IEntityTypeConfiguration<ApplicationConfiguration>
{
    public void Configure(EntityTypeBuilder<ApplicationConfiguration> builder)
    {
        builder.ToTable("ApplicationConfigurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TimeoutSeconds)
            .IsRequired()
            .HasDefaultValue(30);

        builder.Property(c => c.MaxRetries)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(c => c.RagThreshold)
            .IsRequired()
            .HasDefaultValue(0.7);

        builder.Property(c => c.ModelId)
            .HasMaxLength(200);

        builder.Property(c => c.SystemPromptTemplate)
            .HasMaxLength(4000);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ModifiedAt);

        builder.HasOne(c => c.Application)
            .WithMany()
            .HasForeignKey(c => c.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.ApplicationId)
            .IsUnique();
    }
}
