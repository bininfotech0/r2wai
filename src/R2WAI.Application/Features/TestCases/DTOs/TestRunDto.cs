namespace R2WAI.Application.Features.TestCases.DTOs;

public class TestRunDto
{
    public Guid Id { get; init; }
    public Guid? AssistantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public Guid TriggeredByUserId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public int WarningCount { get; init; }
    public List<TestCaseResultDto> Results { get; init; } = new();
}
