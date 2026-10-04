namespace R2WAI.Domain.Tests.Entities;

public class AssistantBehaviorSettingsTests
{
    [Fact]
    public void BuildPromptAddendum_NothingSet_ReturnsNull()
    {
        var settings = new AssistantBehaviorSettings();
        Assert.Null(settings.BuildPromptAddendum());
    }

    [Theory]
    [InlineData("Concise", "Keep your responses concise and to the point.")]
    [InlineData("Detailed", "Provide thorough, detailed responses with explanations.")]
    [InlineData("Friendly", "Respond in a warm, friendly, conversational tone.")]
    [InlineData("Professional", "Respond in a formal, professional tone.")]
    public void BuildPromptAddendum_ResponseStyle_ProducesExpectedInstruction(string style, string expected)
    {
        var settings = new AssistantBehaviorSettings { ResponseStyle = style };
        Assert.Equal(expected, settings.BuildPromptAddendum());
    }

    [Fact]
    public void BuildPromptAddendum_UnrecognizedResponseStyle_ContributesNothing()
    {
        var settings = new AssistantBehaviorSettings { ResponseStyle = "Sarcastic" };
        Assert.Null(settings.BuildPromptAddendum());
    }

    [Theory]
    [InlineData("Brief", "Answer in 1-2 sentences whenever possible.")]
    [InlineData("Thorough", "Give complete, thorough answers rather than the shortest possible response.")]
    public void BuildPromptAddendum_AnswerLength_ProducesExpectedInstruction(string length, string expected)
    {
        var settings = new AssistantBehaviorSettings { AnswerLength = length };
        Assert.Equal(expected, settings.BuildPromptAddendum());
    }

    [Fact]
    public void BuildPromptAddendum_BalancedAnswerLength_ContributesNothing()
    {
        // "Balanced" is the frontend's default/no-op option — must not produce an instruction.
        var settings = new AssistantBehaviorSettings { AnswerLength = "Balanced" };
        Assert.Null(settings.BuildPromptAddendum());
    }

    [Fact]
    public void BuildPromptAddendum_AskClarificationTrue_AddsInstruction()
    {
        var settings = new AssistantBehaviorSettings { AskClarification = true };
        Assert.Contains("ask a clarifying question", settings.BuildPromptAddendum());
    }

    [Fact]
    public void BuildPromptAddendum_AskClarificationFalse_ContributesNothing()
    {
        var settings = new AssistantBehaviorSettings { AskClarification = false };
        Assert.Null(settings.BuildPromptAddendum());
    }

    [Fact]
    public void BuildPromptAddendum_MultipleKnobs_CombinesAllInstructions()
    {
        var settings = new AssistantBehaviorSettings
        {
            ResponseStyle = "Friendly",
            AnswerLength = "Brief",
            AskClarification = true
        };

        var result = settings.BuildPromptAddendum();

        Assert.Contains("friendly", result);
        Assert.Contains("1-2 sentences", result);
        Assert.Contains("clarifying question", result);
    }

    [Fact]
    public void BuildPromptAddendum_IgnoresTemperatureAndMaxOutputTokens()
    {
        // Those two are real model-parameter overrides applied separately by the caller, not prompt text.
        var settings = new AssistantBehaviorSettings { Temperature = 0.2, MaxOutputTokens = 500 };
        Assert.Null(settings.BuildPromptAddendum());
    }
}
