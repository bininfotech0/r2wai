namespace R2WAI.Application.Features.TestCases.DTOs;

public class TestCaseResultDto
{
    public Guid Id { get; init; }
    public Guid TestRunId { get; init; }
    public Guid? TestCaseId { get; init; }
    public string TestCaseName { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ActualResponse { get; init; }
    public long DurationMs { get; init; }
    public string? FunctionCallsJson { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ExecutedAt { get; init; }
}
