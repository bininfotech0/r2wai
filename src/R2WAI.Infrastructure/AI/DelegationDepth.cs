namespace R2WAI.Infrastructure.AI;

/// <summary>
/// Caps how many agent-delegation hops deep the current call stack can go. AsyncLocal, not a
/// DI-scoped service: depth is shaped like the call stack (a parallel delegate_to_agents fan-out
/// must give each branch its own count, starting from the depth the fan-out itself is already at),
/// not like the HTTP request — a scoped singleton would race under Task.WhenAll.
/// </summary>
public static class DelegationDepth
{
    /// <summary>A delegates to B delegates to C delegates to D is already a 4-agent chain for one
    /// user turn; deeper than that is far more likely a loop or a runaway fan-out than a genuine
    /// task decomposition.</summary>
    public const int MaxDepth = 3;

    private static readonly AsyncLocal<int> CurrentDepth = new();

    /// <summary>Null means entering would exceed <see cref="MaxDepth"/> — the caller must not
    /// delegate. Otherwise an IDisposable that restores the prior depth when the delegated call
    /// (and anything it itself delegates to) completes.</summary>
    public static IDisposable? TryEnter()
    {
        var depth = CurrentDepth.Value;
        if (depth >= MaxDepth) return null;
        CurrentDepth.Value = depth + 1;
        return new ExitScope(depth);
    }

    private sealed class ExitScope(int previousDepth) : IDisposable
    {
        public void Dispose() => CurrentDepth.Value = previousDepth;
    }
}
