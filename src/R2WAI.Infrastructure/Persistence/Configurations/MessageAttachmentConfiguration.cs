using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("MessageAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.FileSize)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.MessageId);
    }
}
