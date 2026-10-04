using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("BackgroundJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.JobType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.LeaseOwner)
            .HasMaxLength(100);

        // BackgroundJobProcessor's due-jobs sweep filters on Status + NextAttemptAt every 5s --
        // without this index that's a full table scan against every job ever created, not just
        // the handful currently due.
        builder.HasIndex(j => new { j.Status, j.NextAttemptAt });

        // Same query shape, for the other half of the due-jobs sweep: reclaiming a Processing row
        // whose lease has expired (implementation plan Phase 5).
        builder.HasIndex(j => new { j.Status, j.LeaseExpiresAt });
    }
}
