using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
{
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.ToTable("KnowledgeBases");

        builder.HasKey(kb => kb.Id);

        builder.Property(kb => kb.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(kb => kb.Description)
            .HasMaxLength(2000);

        builder.Property(kb => kb.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(kb => kb.EmbeddingModel)
            .HasMaxLength(200);

        builder.Property(kb => kb.VectorCollectionName)
            .HasMaxLength(200);

        builder.Property(kb => kb.Metadata)
            .HasColumnType("jsonb");

        builder.Property(kb => kb.CreatedAt).IsRequired();
        builder.Property(kb => kb.ModifiedAt);

        builder.HasOne(kb => kb.Tenant)
            .WithMany()
            .HasForeignKey(kb => kb.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(kb => kb.User)
            .WithMany()
            .HasForeignKey(kb => kb.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(kb => kb.Sources)
            .WithOne(s => s.KnowledgeBase)
            .HasForeignKey(s => s.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(kb => kb.Documents)
            .WithOne(d => d.KnowledgeBase)
            .HasForeignKey(d => d.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(kb => new { kb.TenantId, kb.Name });
        builder.HasIndex(kb => new { kb.TenantId, kb.Status });
    }
}
