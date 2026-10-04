using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Application.Common.Interfaces;
using R2WAI.Infrastructure.Security;

namespace R2WAI.Infrastructure.Integrations;

/// <summary>
/// Parses an OpenAPI 3.x document (by URL or raw file content) into the candidate operations
/// ImportOpenApiDialog.tsx already renders. Analysis only — committing a candidate to a real,
/// callable ToolDefinition happens via the existing CreateIntegrationCommand, same as every other
/// integration creation path (see IntegrationsController.CommitOpenApiImport).
/// </summary>
public class OpenApiImportService(IHttpClientFactory httpClientFactory, ILogger<OpenApiImportService> logger)
    : IOpenApiImportService
{
    private const long MaxSpecBytes = 5 * 1024 * 1024;

    public async Task<OpenApiAnalyzeResult> AnalyzeAsync(string? url, string? fileContent, CancellationToken ct = default)
    {
        string content;
        if (!string.IsNullOrWhiteSpace(fileContent))
        {
            content = fileContent;
        }
        else if (!string.IsNullOrWhiteSpace(url))
        {
            content = await FetchSpecAsync(url, ct);
        }
        else
        {
            throw new ValidationException("source", "Either a URL or file content must be provided.");
        }

        var reader = new OpenApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        if (document is null || diagnostic.Errors.Count > 0)
        {
            var reason = diagnostic.Errors.Count > 0 ? diagnostic.Errors[0].Message : "Could not parse document.";
            throw new ValidationException("spec", $"Invalid OpenAPI spec: {reason}");
        }

        var baseUrl = ResolveBaseUrl(document.Servers.FirstOrDefault()?.Url, url);

        var operations = new List<OpenApiOperationCandidate>();
        foreach (var (path, item) in document.Paths)
        {
            foreach (var (method, operation) in item.Operations)
            {
                var suggestedName = !string.IsNullOrWhiteSpace(operation.OperationId)
                    ? operation.OperationId
                    : $"{method}_{path}".Replace('/', '_').Trim('_');

                operations.Add(new OpenApiOperationCandidate(
                    method.ToString().ToUpperInvariant(),
                    path,
                    suggestedName,
                    operation.Summary));
            }
        }

        logger.LogInformation("OpenAPI import analyzed {Count} operations from {Source}", operations.Count, url ?? "uploaded file");
        return new OpenApiAnalyzeResult(baseUrl, operations);
    }

    // OpenAPI server URLs are frequently relative to the spec's own host (e.g. "/api/v3") — resolve
    // those against the source URL's origin so the committed ToolDefinition.EndpointUrl is a real,
    // callable absolute URL rather than a fragment HttpTool can't dispatch.
    private static string ResolveBaseUrl(string? serverUrl, string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            return string.Empty;

        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
            return absolute.ToString().TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(sourceUrl)
            && Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source)
            && Uri.TryCreate(source, serverUrl, out var resolved))
            return resolved.ToString().TrimEnd('/');

        return serverUrl.TrimEnd('/');
    }

    private async Task<string> FetchSpecAsync(string url, CancellationToken ct)
    {
        if (!EgressGuard.IsAllowedUrl(url))
            throw new ValidationException("url", "This URL is not allowed. Internal network addresses are blocked.");

        var client = httpClientFactory.CreateClient("HttpTool");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        using var response = await client.GetAsync(url, cts.Token);
        if (!response.IsSuccessStatusCode)
            throw new ValidationException("url", $"Failed to fetch spec: HTTP {(int)response.StatusCode}.");

        if (response.Content.Headers.ContentLength is { } len && len > MaxSpecBytes)
            throw new ValidationException("url", "Spec file is too large (max 5 MB).");

        return await response.Content.ReadAsStringAsync(cts.Token);
    }
}
