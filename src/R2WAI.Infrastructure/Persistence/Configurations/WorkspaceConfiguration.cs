using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(w => w.Description)
            .HasMaxLength(2000);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.ModifiedAt);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(w => w.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.TenantId, w.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // At most one live default per tenant: AssignDefaultWorkspaces relies on finding exactly one.
        builder.HasIndex(w => w.TenantId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false")
            .HasDatabaseName("IX_Workspaces_TenantId_Default");

        // Restrict, never cascade: deleting a workspace must not silently delete what is in it.
        builder.HasMany<AssistantDefinition>().WithOne().HasForeignKey(a => a.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<KnowledgeBase>().WithOne().HasForeignKey(k => k.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<Workflow>().WithOne().HasForeignKey(w => w.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<Chatbot>().WithOne().HasForeignKey(c => c.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<ConnectedApplication>().WithOne().HasForeignKey(a => a.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
