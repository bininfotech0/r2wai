namespace R2WAI.Application.Common.Models;

public record FunctionCallTraceDto(
    string Plugin,
    string Function,
    string? Arguments,
    long DurationMs,
    bool Success,
    string? Error);
