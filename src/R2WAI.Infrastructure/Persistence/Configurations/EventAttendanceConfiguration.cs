using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class EventAttendanceConfiguration : IEntityTypeConfiguration<EventAttendance>
{
    public void Configure(EntityTypeBuilder<EventAttendance> builder)
    {
        builder.ToTable("EventAttendances");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PointsAwarded).IsRequired();
        builder.Property(a => a.AwardedAt).IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ModifiedAt);

        builder.HasOne(a => a.Event)
            .WithMany(e => e.Attendances)
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A member can only be credited once per event.
        builder.HasIndex(a => new { a.EventId, a.UserId }).IsUnique();
    }
}
