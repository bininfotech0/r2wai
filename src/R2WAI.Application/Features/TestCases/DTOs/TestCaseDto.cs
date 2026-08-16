namespace R2WAI.Application.Features.TestCases.DTOs;

public class TestCaseDto
{
    public Guid Id { get; init; }
    public Guid AssistantId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string? ExpectedResponseContains { get; init; }
    public string? ExpectedCapabilityCalled { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
}
