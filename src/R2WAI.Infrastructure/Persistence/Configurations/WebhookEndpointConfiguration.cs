using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public sealed class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        // Public webhook URLs contain only the slug, so it must identify exactly one tenant-owned
        // endpoint across the whole installation. Soft-deleted endpoints may release their slug.
        builder.HasIndex(endpoint => endpoint.Slug)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");
    }
}
