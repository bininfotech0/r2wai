using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ChatbotSessionTurnConfiguration : IEntityTypeConfiguration<ChatbotSessionTurn>
{
    public void Configure(EntityTypeBuilder<ChatbotSessionTurn> builder)
    {
        builder.ToTable("ChatbotSessionTurns");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.SessionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.Chatbot)
            .WithMany()
            .HasForeignKey(t => t.ChatbotId)
            .OnDelete(DeleteBehavior.Cascade);

        // The one read path: "latest N turns of this chatbot's session".
        builder.HasIndex(t => new { t.ChatbotId, t.SessionId, t.CreatedAt });
        // The retention sweep's range delete.
        builder.HasIndex(t => t.CreatedAt);
    }
}
