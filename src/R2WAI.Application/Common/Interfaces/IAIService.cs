namespace R2WAI.Application.Common.Interfaces;

public interface IAIService
{
    Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null, string? context = null, CancellationToken ct = default);
    Task<string> SummarizeTextAsync(string text, int maxLength = 500, CancellationToken ct = default);
    Task<string> ExtractDataAsync(string text, string schema, CancellationToken ct = default);
    Task<string> CompareDocumentsAsync(string sourceText, string targetText, CancellationToken ct = default);
    Task<string> ChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamChatAsync(string message, string? conversationHistory = null, string? systemPrompt = null, CancellationToken ct = default);
    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<IReadOnlyList<float>>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);
    Task<string> AnswerQuestionAsync(string question, string context, CancellationToken ct = default);
}
