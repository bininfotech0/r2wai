using System.Diagnostics;
using System.Text.Json;

namespace R2WAI.Infrastructure.Services.ToolFramework;

public sealed class HttpToolOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class HttpTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly HttpToolOptions _options;
    private readonly ILogger<HttpTool> _logger;

    public string Name => "HttpTool";
    public string Description => "Makes HTTP requests to external APIs";

    public HttpTool(HttpClient httpClient, HttpToolOptions options, ILogger<HttpTool> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            context.Parameters.TryGetValue("method", out var methodObj);
            context.Parameters.TryGetValue("path", out var pathObj);
            context.Parameters.TryGetValue("body", out var bodyObj);
            context.Parameters.TryGetValue("baseUrl", out var baseUrlObj);
            context.Parameters.TryGetValue("authorizationHeader", out var authHeaderObj);
            context.Parameters.TryGetValue("extraHeaderName", out var extraHeaderNameObj);
            context.Parameters.TryGetValue("extraHeaderValue", out var extraHeaderValueObj);

            var method = methodObj?.ToString() ?? "GET";
            var path = pathObj?.ToString() ?? "";
            var baseUrl = baseUrlObj as string ?? _options.BaseUrl;
            var url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

            _logger.LogInformation("HttpTool executing {Method} {Url}", method, url);

            // A body that's already a JSON string (e.g. from a dynamically-built tool call) is sent
            // as-is; anything else is serialized.
            var content = bodyObj switch
            {
                null => null,
                string raw when !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) => raw,
                _ when !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) => JsonSerializer.Serialize(bodyObj),
                _ => null
            };

            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (content is not null)
                request.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
            if (authHeaderObj is string authHeader && !string.IsNullOrWhiteSpace(authHeader))
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            if (extraHeaderNameObj is string extraHeaderName && !string.IsNullOrWhiteSpace(extraHeaderName)
                && extraHeaderValueObj is string extraHeaderValue && !string.IsNullOrWhiteSpace(extraHeaderValue))
                request.Headers.TryAddWithoutValidation(extraHeaderName, extraHeaderValue);

            var response = await _httpClient.SendAsync(request, context.CancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);
            sw.Stop();

            string? mappedData = null;
            if (context.Parameters.TryGetValue("responseMappings", out var mappingsObj) && mappingsObj is not null)
            {
                try
                {
                    var mappingsJson = mappingsObj.ToString()!;
                    var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingsJson) ?? [];
                    var extracted = ResponseMapper.ExtractMappings(responseBody, mappings);
                    mappedData = JsonSerializer.Serialize(extracted);
                }
                catch { }
            }

            return new ToolResult
            {
                Success = response.IsSuccessStatusCode,
                Data = mappedData ?? responseBody,
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "HttpTool execution failed");
            return new ToolResult
            {
                Success = false,
                Error = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }
}
