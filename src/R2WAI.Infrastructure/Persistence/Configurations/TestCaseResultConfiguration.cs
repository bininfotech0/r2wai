using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class TestCaseResultConfiguration : IEntityTypeConfiguration<TestCaseResult>
{
    public void Configure(EntityTypeBuilder<TestCaseResult> builder)
    {
        builder.ToTable("TestCaseResults");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TestCaseName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.Question)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.ActualResponse)
            .HasColumnType("text");

        builder.Property(r => r.FunctionCallsJson)
            .HasColumnType("jsonb");

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(r => r.ExecutedAt).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ModifiedAt);

        builder.HasOne(r => r.TestCase)
            .WithMany()
            .HasForeignKey(r => r.TestCaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => r.TestRunId);
        builder.HasIndex(r => r.TestCaseId);
    }
}
