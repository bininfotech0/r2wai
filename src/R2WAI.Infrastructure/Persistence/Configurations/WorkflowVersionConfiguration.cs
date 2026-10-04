using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.ToTable("WorkflowVersions");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VersionNumber)
            .IsRequired();

        builder.Property(v => v.ConfigSnapshot)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(v => v.IsPublished)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.ModifiedAt);

        builder.HasOne(v => v.Workflow)
            .WithMany()
            .HasForeignKey(v => v.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.WorkflowId, v.VersionNumber })
            .IsUnique();
    }
}
