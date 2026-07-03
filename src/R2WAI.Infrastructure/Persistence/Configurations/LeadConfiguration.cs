using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.Phone).HasMaxLength(30);
        builder.Property(l => l.Email).HasMaxLength(320);
        builder.Property(l => l.ClassOrGrade).HasMaxLength(50);
        builder.Property(l => l.Interest).HasMaxLength(500);
        builder.Property(l => l.Source).HasMaxLength(100);
        builder.Property(l => l.Notes).HasMaxLength(2000);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.ModifiedAt);

        builder.HasOne(l => l.Tenant)
            .WithMany()
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Chatbot)
            .WithMany()
            .HasForeignKey(l => l.ChatbotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => new { l.TenantId, l.Status });
        builder.HasIndex(l => new { l.TenantId, l.CreatedAt });
    }
}
