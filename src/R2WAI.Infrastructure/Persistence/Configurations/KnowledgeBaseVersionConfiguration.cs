using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class KnowledgeBaseVersionConfiguration : IEntityTypeConfiguration<KnowledgeBaseVersion>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseVersion> builder)
    {
        builder.ToTable("KnowledgeBaseVersions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.ConfigSnapshot)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(v => v.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.Note)
            .HasMaxLength(1000);

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.ModifiedAt);

        builder.HasOne(v => v.KnowledgeBase)
            .WithMany()
            .HasForeignKey(v => v.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.KnowledgeBaseId, v.VersionNumber })
            .IsUnique();
    }
}
