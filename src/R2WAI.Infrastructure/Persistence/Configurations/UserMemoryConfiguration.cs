using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class UserMemoryConfiguration : IEntityTypeConfiguration<UserMemory>
{
    public void Configure(EntityTypeBuilder<UserMemory> builder)
    {
        builder.ToTable("UserMemories");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Category).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Source).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Key).HasMaxLength(UserMemory.MaxKeyLength);
        builder.Property(m => m.Content).IsRequired().HasMaxLength(UserMemory.MaxContentLength);

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.ModifiedAt);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);
        // A user's memories go with the user; an agent's partition goes with the agent.
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AssistantDefinition>().WithMany().HasForeignKey(m => m.AssistantId).OnDelete(DeleteBehavior.Cascade);

        // The read path: one user's memories, optionally narrowed to a partition.
        builder.HasIndex(m => new { m.UserId, m.AssistantId, m.Status });

        // A key names one memory per partition. Two filtered indexes because Postgres treats NULLs
        // as distinct, so a single (UserId, AssistantId, Key) index would not cover the Personal
        // partition (AssistantId null).
        builder.HasIndex(m => new { m.UserId, m.Key })
            .IsUnique()
            .HasFilter("\"AssistantId\" IS NULL AND \"Key\" IS NOT NULL")
            .HasDatabaseName("IX_UserMemories_UserId_Key_Personal");
        builder.HasIndex(m => new { m.UserId, m.AssistantId, m.Key })
            .IsUnique()
            .HasFilter("\"AssistantId\" IS NOT NULL AND \"Key\" IS NOT NULL")
            .HasDatabaseName("IX_UserMemories_UserId_AssistantId_Key");
    }
}

public class UserMemorySettingsConfiguration : IEntityTypeConfiguration<UserMemorySettings>
{
    public void Configure(EntityTypeBuilder<UserMemorySettings> builder)
    {
        builder.ToTable("UserMemorySettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.MemoryEnabled).IsRequired();
        builder.Property(s => s.AutoLearnEnabled).IsRequired();
        builder.Property(s => s.ReviewLearned).IsRequired();
        builder.Property(s => s.AgentNotesEnabled).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ModifiedAt);

        builder.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId).IsUnique();
    }
}
