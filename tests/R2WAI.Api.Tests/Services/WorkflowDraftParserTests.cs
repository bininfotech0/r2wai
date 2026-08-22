using R2WAI.Api.Services;

namespace R2WAI.Api.Tests.Services;

/// <summary>
/// Covers WorkflowDraftParser — the pure JSON-extraction/vocabulary-validation logic behind the New
/// Automation wizard's "what do you want to automate?" step. No HTTP/IAIService involved, so this
/// runs against arbitrary (including malformed or adversarial) model output directly.
/// </summary>
public class WorkflowDraftParserTests
{
    private static readonly string[] Triggers = ["Application Submitted", "Application Updated", "Schedule"];
    private static readonly string[] Actions = ["Verify Documents", "Assign Officer", "Send Notification"];

    [Fact]
    public void Parse_NullOrWhitespace_ReturnsEmptyDraft()
    {
        var result = WorkflowDraftParser.Parse(null, Triggers, Actions);
        Assert.Null(result.Trigger);
        Assert.Empty(result.Actions);
        Assert.Empty(result.Conditions);
    }

    [Fact]
    public void Parse_ValidJson_ExtractsMatchingFields()
    {
        var raw = """
        {
          "name": "Onboarding Automation",
          "trigger": "Application Submitted",
          "actions": ["Verify Documents", "Assign Officer"],
          "conditions": [{"field": "Status", "operator": "Is equal to", "value": "Pending"}]
        }
        """;

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Equal("Onboarding Automation", result.Name);
        Assert.Equal("Application Submitted", result.Trigger);
        Assert.Equal(["Verify Documents", "Assign Officer"], result.Actions);
        Assert.Single(result.Conditions);
        Assert.Equal("Status", result.Conditions[0].Field);
    }

    [Fact]
    public void Parse_StripsMarkdownCodeFences()
    {
        var raw = "```json\n{\"trigger\": \"Schedule\", \"actions\": []}\n```";

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Equal("Schedule", result.Trigger);
    }

    [Fact]
    public void Parse_TriggerNotInVocabulary_DiscardedAsNull()
    {
        // The model hallucinated a trigger that isn't one of the options it was given.
        var raw = """{"trigger": "Mystery Trigger", "actions": []}""";

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Null(result.Trigger);
    }

    [Fact]
    public void Parse_TriggerMatch_IsCaseInsensitive()
    {
        var raw = """{"trigger": "application submitted", "actions": []}""";

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Equal("Application Submitted", result.Trigger); // normalized to the canonical casing
    }

    [Fact]
    public void Parse_ActionsNotInVocabulary_AreFilteredOut()
    {
        // Mix of a valid action and a hallucinated one — only the valid one should survive.
        var raw = """{"actions": ["Verify Documents", "Launch Nuclear Missiles"]}""";

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Equal(["Verify Documents"], result.Actions);
    }

    [Fact]
    public void Parse_ConditionWithBlankField_IsDropped()
    {
        var raw = """{"conditions": [{"field": "", "operator": "Contains", "value": "x"}]}""";

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Empty(result.Conditions);
    }

    [Fact]
    public void Parse_ConditionMissingOperator_DefaultsToIsEqualTo()
    {
        var raw = """{"conditions": [{"field": "Status", "value": "Pending"}]}""";

        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Equal("Is equal to", result.Conditions[0].Operator);
    }

    [Theory]
    [InlineData("not valid json at all")]
    [InlineData("{ unterminated")]
    [InlineData("")]
    public void Parse_MalformedInput_ReturnsEmptyDraft_DoesNotThrow(string raw)
    {
        var result = WorkflowDraftParser.Parse(raw, Triggers, Actions);

        Assert.Null(result.Trigger);
        Assert.Empty(result.Actions);
        Assert.Empty(result.Conditions);
    }
}
