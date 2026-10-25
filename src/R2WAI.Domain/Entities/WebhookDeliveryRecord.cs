using R2WAI.Domain.Common;

namespace R2WAI.Domain.Entities;

/// <summary>
/// A short-lived idempotency claim for an authenticated webhook delivery. The key is a SHA-256
/// digest, not the provider's raw delivery header, and is unique within one webhook endpoint.
/// </summary>
public sealed class WebhookDeliveryRecord : BaseEntity<Guid>
{
    public Guid WebhookEndpointId { get; private set; }
    public string DeliveryKeyHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }

    private WebhookDeliveryRecord() { }

    public WebhookDeliveryRecord(Guid id, Guid webhookEndpointId, string deliveryKeyHash, DateTime expiresAt)
    {
        Id = id;
        WebhookEndpointId = webhookEndpointId;
        DeliveryKeyHash = deliveryKeyHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }
}
