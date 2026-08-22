namespace R2WAI.Infrastructure.AI.ModelGateway;

/// <summary>
/// Shared client tuning, split by provider latency profile. See the timeout rationale on each
/// constant's usage site: a cloud provider's normal replies fit well inside 45s, while a
/// self-hosted Ollama model doing real CPU-bound inference has legitimately been observed taking
/// 82-94s, so it needs materially more room before a slow-but-correct reply is treated as a hang.
/// </summary>
internal static class ModelGatewayDefaults
{
    public static readonly TimeSpan CloudNetworkTimeout = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan OllamaNetworkTimeout = TimeSpan.FromSeconds(150);
}
