using R2WAI.Application.Common.Models;

namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Scoped, per-request collector of function/tool calls the AI actually invokes during a
/// single chat turn, so a real execution trace can be shown (Test Studio) instead of a
/// fabricated one. Populated by AiFunctionAuditFilter, read by ChatWithAssistantCommand.
/// </summary>
public interface IChatTraceCollector
{
    void RecordFunctionCall(string plugin, string function, string? arguments, long durationMs, bool success, string? error);
    IReadOnlyList<FunctionCallTraceDto> GetTrace();
    void Clear();
}
