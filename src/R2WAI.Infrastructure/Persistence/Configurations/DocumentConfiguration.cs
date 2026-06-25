using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.Description)
            .HasMaxLength(2000);

        builder.Property(d => d.FileType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.FileSize)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.ProcessingError)
            .HasMaxLength(2000);

        builder.Property(d => d.VectorIds)
            .HasColumnType("jsonb");

        builder.Property(d => d.Metadata)
            .HasColumnType("jsonb");

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.ModifiedAt);

        builder.HasOne(d => d.Tenant)
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.KnowledgeBase)
            .WithMany(kb => kb.Documents)
            .HasForeignKey(d => d.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => new { d.TenantId, d.KnowledgeBaseId });
        builder.HasIndex(d => new { d.TenantId, d.Status });
    }
}
