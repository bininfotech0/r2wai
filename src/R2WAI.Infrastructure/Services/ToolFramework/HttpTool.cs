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

            var method = methodObj?.ToString() ?? "GET";
            var path = pathObj?.ToString() ?? "";
            var url = _options.BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

            _logger.LogInformation("HttpTool executing {Method} {Url}", method, url);

            HttpResponseMessage response;
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var content = bodyObj is not null ? JsonSerializer.Serialize(bodyObj) : null;
                response = await _httpClient.PostAsync(url, content is not null ? new StringContent(content, System.Text.Encoding.UTF8, "application/json") : null, context.CancellationToken);
            }
            else
            {
                response = await _httpClient.GetAsync(url, context.CancellationToken);
            }

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
