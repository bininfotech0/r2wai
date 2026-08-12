using R2WAI.Application.Common.Interfaces;
using R2WAI.Application.Common.Models;

namespace R2WAI.Infrastructure.AI;

public sealed class ChatTraceCollector : IChatTraceCollector
{
    private readonly List<FunctionCallTraceDto> _calls = new();

    public void RecordFunctionCall(string plugin, string function, string? arguments, long durationMs, bool success, string? error) =>
        _calls.Add(new FunctionCallTraceDto(plugin, function, arguments, durationMs, success, error));

    public IReadOnlyList<FunctionCallTraceDto> GetTrace() => _calls.AsReadOnly();

    public void Clear() => _calls.Clear();
}
