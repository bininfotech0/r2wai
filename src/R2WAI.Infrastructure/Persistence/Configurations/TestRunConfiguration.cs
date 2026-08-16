using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class TestRunConfiguration : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> builder)
    {
        builder.ToTable("TestRuns");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.StartedAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ModifiedAt);

        builder.HasOne(t => t.Tenant)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Assistant)
            .WithMany()
            .HasForeignKey(t => t.AssistantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Application)
            .WithMany()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Results)
            .WithOne(r => r.TestRun)
            .HasForeignKey(r => r.TestRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.TenantId, t.AssistantId });
        builder.HasIndex(t => new { t.TenantId, t.ApplicationId });
    }
}
