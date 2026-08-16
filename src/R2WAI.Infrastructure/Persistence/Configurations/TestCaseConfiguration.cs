using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("TestCases");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.Question)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(t => t.ExpectedResponseContains)
            .HasMaxLength(1000);

        builder.Property(t => t.ExpectedCapabilityCalled)
            .HasMaxLength(500);

        builder.Property(t => t.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Assistant)
            .WithMany()
            .HasForeignKey(t => t.AssistantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.Application)
            .WithMany()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => new { t.TenantId, t.AssistantId });
        builder.HasIndex(t => t.ApplicationId);
    }
}
