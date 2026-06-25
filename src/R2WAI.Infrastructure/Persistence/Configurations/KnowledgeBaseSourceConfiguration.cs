using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class KnowledgeBaseSourceConfiguration : IEntityTypeConfiguration<KnowledgeBaseSource>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseSource> builder)
    {
        builder.ToTable("KnowledgeBaseSources");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Url)
            .HasMaxLength(2000);

        builder.Property(s => s.Content)
            .HasColumnType("text");

        builder.Property(s => s.Status)
            .HasMaxLength(50);

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ModifiedAt);

        builder.HasOne(s => s.KnowledgeBase)
            .WithMany(kb => kb.Sources)
            .HasForeignKey(s => s.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.KnowledgeBaseId);
    }
}
