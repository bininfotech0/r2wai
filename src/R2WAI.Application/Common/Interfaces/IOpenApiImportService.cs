namespace R2WAI.Application.Common.Interfaces;

public record OpenApiOperationCandidate(string Method, string Path, string SuggestedName, string? Summary);

public record OpenApiAnalyzeResult(string BaseUrl, IReadOnlyList<OpenApiOperationCandidate> Operations);

public interface IOpenApiImportService
{
    Task<OpenApiAnalyzeResult> AnalyzeAsync(string? url, string? fileContent, CancellationToken ct = default);
}
