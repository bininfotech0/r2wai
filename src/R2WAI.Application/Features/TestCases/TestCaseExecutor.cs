using System.Diagnostics;
using System.Text.Json;
using R2WAI.Application.Features.Assistants.Commands;

namespace R2WAI.Application.Features.TestCases;

/// Shared by RunTestCase and RunAllTestCases so both execute assistants the same real way
/// ChatWithAssistantCommand does elsewhere (Playground, workspace Test tab) — no fabricated traces.
internal static class TestCaseExecutor
{
    public static async Task<TestCaseResult> ExecuteAsync(IMediator mediator, Guid testRunId, TestCase testCase, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var chatResult = await mediator.Send(new ChatWithAssistantCommand
            {
                AssistantId = testCase.AssistantId,
                Message = testCase.Question
            }, ct);
            stopwatch.Stop();

            var functionCallsJson = chatResult.FunctionCalls is { Count: > 0 }
                ? JsonSerializer.Serialize(chatResult.FunctionCalls)
                : null;

            var (status, error) = Evaluate(testCase, chatResult);

            return new TestCaseResult(Guid.NewGuid(), testRunId, testCase.Id, testCase.Name, testCase.Question,
                status, chatResult.Reply, stopwatch.ElapsedMilliseconds, functionCallsJson, error);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TestCaseResult(Guid.NewGuid(), testRunId, testCase.Id, testCase.Name, testCase.Question,
                TestCaseResultStatus.Failed, null, stopwatch.ElapsedMilliseconds, null, ex.Message);
        }
    }

    private static (TestCaseResultStatus Status, string? Error) Evaluate(TestCase testCase, ChatWithAssistantResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Reply))
            return (TestCaseResultStatus.Failed, "Assistant returned an empty response.");

        if (!string.IsNullOrWhiteSpace(testCase.ExpectedResponseContains) &&
            !result.Reply.Contains(testCase.ExpectedResponseContains, StringComparison.OrdinalIgnoreCase))
            return (TestCaseResultStatus.Failed, $"Expected response to contain \"{testCase.ExpectedResponseContains}\".");

        if (!string.IsNullOrWhiteSpace(testCase.ExpectedCapabilityCalled))
        {
            var called = result.FunctionCalls?.Any(f =>
                f.Function.Contains(testCase.ExpectedCapabilityCalled, StringComparison.OrdinalIgnoreCase) ||
                f.Plugin.Contains(testCase.ExpectedCapabilityCalled, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (!called)
                return (TestCaseResultStatus.Warning, $"Expected capability \"{testCase.ExpectedCapabilityCalled}\" was not called.");
        }

        return (TestCaseResultStatus.Passed, null);
    }
}
