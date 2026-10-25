using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace R2WAI.Infrastructure.Persistence.Configurations;

public sealed class WebhookDeliveryRecordConfiguration : IEntityTypeConfiguration<WebhookDeliveryRecord>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryRecord> builder)
    {
        builder.ToTable("WebhookDeliveryRecords");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.DeliveryKeyHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(record => record.ExpiresAt).IsRequired();
        builder.HasIndex(record => new { record.WebhookEndpointId, record.DeliveryKeyHash }).IsUnique();
        builder.HasIndex(record => record.ExpiresAt);
    }
}
