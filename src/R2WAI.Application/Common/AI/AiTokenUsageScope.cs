namespace R2WAI.Application.Common.AI;

/// <summary>
/// Collects the token counts the model provider reports for every completion made while the scope is
/// open, so a chat path can store them on the reply message without IAIService returning them.
/// The holder object is shared through AsyncLocal: the AI service adds to the same instance the
/// caller opened, so the total is visible after the awaited call returns.
/// </summary>
public sealed class AiTokenUsageScope : IDisposable
{
    private static readonly AsyncLocal<AiTokenUsageScope?> CurrentScope = new();

    private readonly AiTokenUsageScope? _parent;
    private int _totalTokens;
    private bool _anyReported;

    private AiTokenUsageScope(AiTokenUsageScope? parent) => _parent = parent;

    public static AiTokenUsageScope Begin()
    {
        var scope = new AiTokenUsageScope(CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    /// <summary>Total tokens reported inside this scope, or null when the provider reported none.</summary>
    public int? TotalTokens => _anyReported ? _totalTokens : null;

    /// <summary>Called by the AI service after each completion that carries usage data.</summary>
    public static void Report(int tokens)
    {
        for (var scope = CurrentScope.Value; scope is not null; scope = scope._parent)
        {
            Interlocked.Add(ref scope._totalTokens, tokens);
            scope._anyReported = true;
        }
    }

    public void Dispose() => CurrentScope.Value = _parent;
}
