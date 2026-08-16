using R2WAI.Domain.Common;
using R2WAI.Domain.Enums;

namespace R2WAI.Domain.Entities;

public sealed class TestCaseResult : BaseEntity<Guid>
{
    public Guid TestRunId { get; private set; }
    public Guid? TestCaseId { get; private set; }
    public string TestCaseName { get; private set; } = string.Empty;
    public string Question { get; private set; } = string.Empty;
    public TestCaseResultStatus Status { get; private set; }
    public string? ActualResponse { get; private set; }
    public long DurationMs { get; private set; }
    public string? FunctionCallsJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime ExecutedAt { get; private set; }

    public TestRun TestRun { get; private set; } = null!;
    public TestCase? TestCase { get; private set; }

    private TestCaseResult() { }

    public TestCaseResult(Guid id, Guid testRunId, Guid? testCaseId, string testCaseName, string question,
        TestCaseResultStatus status, string? actualResponse, long durationMs,
        string? functionCallsJson = null, string? errorMessage = null)
    {
        Id = id;
        TestRunId = testRunId;
        TestCaseId = testCaseId;
        TestCaseName = testCaseName;
        Question = question;
        Status = status;
        ActualResponse = actualResponse;
        DurationMs = durationMs;
        FunctionCallsJson = functionCallsJson;
        ErrorMessage = errorMessage;
        ExecutedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }
}
