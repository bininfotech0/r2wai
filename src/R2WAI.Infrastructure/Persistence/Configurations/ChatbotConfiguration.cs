using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class ChatbotConfiguration : IEntityTypeConfiguration<Chatbot>
{
    public void Configure(EntityTypeBuilder<Chatbot> builder)
    {
        builder.ToTable("Chatbots");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.Description)
            .HasMaxLength(2000);

        builder.Property(c => c.WelcomeMessage)
            .HasMaxLength(2000);

        builder.Property(c => c.SuggestedQuestions)
            .HasColumnType("jsonb");

        builder.Property(c => c.PromptTemplate)
            .HasColumnType("text");

        builder.Property(c => c.Settings)
            .HasColumnType("jsonb");

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ModifiedAt);

        builder.HasOne(c => c.Tenant)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.KnowledgeBase)
            .WithMany()
            .HasForeignKey(c => c.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ModelConfiguration)
            .WithMany()
            .HasForeignKey(c => c.ModelConfigurationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.TenantId, c.Name });
    }
}
