using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Infrastructure.AI;

public sealed class EnabledToolScope : IEnabledToolScope
{
    public IReadOnlyCollection<Guid>? EnabledToolIds { get; set; }
}
