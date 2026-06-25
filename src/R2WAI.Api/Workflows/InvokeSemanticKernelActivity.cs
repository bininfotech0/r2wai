using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using R2WAI.Application.Common.Interfaces;

namespace R2WAI.Api.Workflows;

[Activity("R2WAI", "AI", "Invokes a Semantic Kernel AI function and returns the result.")]
public class InvokeSemanticKernelActivity : Activity<string>
{
    [Input(Description = "The prompt template to send. Use {{Input}} to reference the input variable, or Liquid syntax for dynamic values.")]
    public Input<string> Prompt { get; set; } = default!;

    [Input(Description = "Optional system prompt for context.")]
    public Input<string?> SystemPrompt { get; set; } = default!;

    [Input(Description = "Optional context data to include in the prompt.")]
    public Input<string?> Context { get; set; } = default!;

    [Input(Description = "Maximum tokens for the response (default 1024).")]
    public Input<int> MaxTokens { get; set; } = new(1024);

    [Input(Description = "Temperature for response generation (0.0 - 1.0, default 0.7).")]
    public Input<double> Temperature { get; set; } = new(0.7);

    [Output(Description = "The AI-generated response text.")]
    public Output<string> Response { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var prompt = context.Get(Prompt) ?? string.Empty;
        var systemPrompt = context.Get(SystemPrompt);
        var ctx = context.Get(Context);
        var maxTokens = context.Get(MaxTokens);
        var temperature = context.Get(Temperature);

        if (string.IsNullOrEmpty(prompt))
        {
            context.Set(Response, string.Empty);
            context.Set(Result, string.Empty);
            await context.CompleteActivityAsync();
            return;
        }

        var aiService = context.GetRequiredService<IAIService>();

        var fullPrompt = BuildPrompt(prompt, systemPrompt, ctx);

        var result = await aiService.GenerateResponseAsync(fullPrompt, systemPrompt, ctx, context.CancellationToken);

        context.Set(Response, result);
        context.Set(Result, result);
        await context.CompleteActivityAsync();
    }

    private static string BuildPrompt(string prompt, string? systemPrompt, string? context)
    {
        if (!string.IsNullOrEmpty(systemPrompt) || !string.IsNullOrEmpty(context))
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(systemPrompt))
                parts.Add($"System: {systemPrompt}");
            if (!string.IsNullOrEmpty(context))
                parts.Add($"Context:\n{context}");
            parts.Add($"User: {prompt}");
            return string.Join("\n\n", parts);
        }
        return prompt;
    }
}
