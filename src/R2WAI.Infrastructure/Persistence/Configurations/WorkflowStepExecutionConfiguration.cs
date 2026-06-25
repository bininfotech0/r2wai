using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WorkflowStepExecutionConfiguration : IEntityTypeConfiguration<WorkflowStepExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowStepExecution> builder)
    {
        builder.ToTable("WorkflowStepExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.StepName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StepType).HasMaxLength(100);
        builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Output).HasColumnType("text");
        builder.Property(e => e.Error).HasColumnType("text");
        builder.Property(e => e.Variables).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.WorkflowInstanceId, e.StepIndex });

        builder.HasOne(e => e.WorkflowInstance)
            .WithMany()
            .HasForeignKey(e => e.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
