using System.Text.Json;

namespace R2WAI.Infrastructure.Services.ToolFramework;

public static class ResponseMapper
{
    public static Dictionary<string, string?> ExtractMappings(string jsonResponse, Dictionary<string, string> mappings)
    {
        var result = new Dictionary<string, string?>();

        if (string.IsNullOrEmpty(jsonResponse) || mappings.Count == 0)
            return result;

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);

            foreach (var (variableName, jsonPath) in mappings)
            {
                var value = ExtractValue(doc.RootElement, jsonPath);
                result[variableName] = value;
            }
        }
        catch (JsonException)
        {
            foreach (var (variableName, _) in mappings)
                result[variableName] = null;
        }

        return result;
    }

    private static string? ExtractValue(JsonElement element, string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = element;

        foreach (var segment in segments)
        {
            if (segment.EndsWith(']'))
            {
                var bracketIdx = segment.IndexOf('[');
                if (bracketIdx > 0)
                {
                    var propName = segment[..bracketIdx];
                    var indexStr = segment[(bracketIdx + 1)..^1];

                    if (!current.TryGetProperty(propName, out current))
                        return null;

                    if (current.ValueKind != JsonValueKind.Array)
                        return null;

                    if (!int.TryParse(indexStr, out var index) || index >= current.GetArrayLength())
                        return null;

                    current = current[index];
                    continue;
                }
            }

            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText(),
        };
    }
}
