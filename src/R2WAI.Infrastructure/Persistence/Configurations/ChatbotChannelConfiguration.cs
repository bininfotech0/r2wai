using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ChatbotChannelConfiguration : IEntityTypeConfiguration<ChatbotChannel>
{
    public void Configure(EntityTypeBuilder<ChatbotChannel> builder)
    {
        builder.ToTable("ChatbotChannels");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ChannelType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.EncryptedCredentials)
            .HasColumnType("text");

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ModifiedAt);

        builder.HasOne(c => c.Chatbot)
            .WithMany()
            .HasForeignKey(c => c.ChatbotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.TenantId, c.ChatbotId, c.ChannelType }).IsUnique();
    }
}
