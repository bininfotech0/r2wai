using MediatR;

namespace R2WAI.Domain.Common;

public interface IBaseDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}

public abstract class BaseDomainEvent : IBaseDomainEvent
{
    public Guid EventId { get; protected set; } = Guid.NewGuid();
    public DateTime OccurredOn { get; protected set; } = DateTime.UtcNow;
}
