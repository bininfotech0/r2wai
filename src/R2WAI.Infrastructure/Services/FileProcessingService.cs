using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace R2WAI.Infrastructure.Services;

public partial class FileProcessingService
{
    private readonly ILogger<FileProcessingService> _logger;

    public FileProcessingService(ILogger<FileProcessingService> logger)
    {
        _logger = logger;
    }

    public Task<string> ExtractTextFromPdfAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            var text = ExtractFromPdfCore(filePath);
            return Task.FromResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF extraction failed for {Path}", filePath);
            var size = new FileInfo(filePath).Length;
            return Task.FromResult($"[PDF file: {Path.GetFileName(filePath)}, size: {size} bytes]");
        }
    }

    public Task<string> ExtractTextFromDocxAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null)
                return Task.FromResult($"[DOCX file: {Path.GetFileName(filePath)}]");

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var xml = reader.ReadToEnd();

            var result = new StringBuilder();
            foreach (Match match in WtTagRegex().Matches(xml))
            {
                result.Append(match.Groups[1].Value);
                result.Append(' ');
            }

            var text = result.ToString().Trim();
            return Task.FromResult(!string.IsNullOrWhiteSpace(text) ? text : $"[DOCX file: {Path.GetFileName(filePath)}]");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DOCX extraction failed for {Path}", filePath);
            return Task.FromResult($"[DOCX file: {Path.GetFileName(filePath)}]");
        }
    }

    public Task<string> ExtractTextFromXlsxAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);

            var sharedStrings = new List<string>();
            var ssEntry = zip.GetEntry("xl/sharedStrings.xml");
            if (ssEntry is not null)
            {
                using var stream = ssEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var xml = reader.ReadToEnd();
                foreach (Match match in SiTTagRegex().Matches(xml))
                    sharedStrings.Add(match.Groups[1].Value);
            }

            var result = new StringBuilder();
            for (var i = 1; i <= 20; i++)
            {
                var sheetEntry = zip.GetEntry($"xl/worksheets/sheet{i}.xml");
                if (sheetEntry is null) break;

                using var stream = sheetEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var xml = reader.ReadToEnd();

                foreach (Match match in CellValueRegex().Matches(xml))
                {
                    var cellType = match.Groups[1].Value;
                    var value = match.Groups[2].Value;

                    if (cellType == "s" && int.TryParse(value, out var idx) && idx < sharedStrings.Count)
                        result.Append(sharedStrings[idx]);
                    else
                        result.Append(value);

                    result.Append('\t');
                }
                result.AppendLine();
            }

            var text = result.ToString().Trim();
            return Task.FromResult(!string.IsNullOrWhiteSpace(text) ? text : $"[XLSX file: {Path.GetFileName(filePath)}]");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XLSX extraction failed for {Path}", filePath);
            return Task.FromResult($"[XLSX file: {Path.GetFileName(filePath)}]");
        }
    }

    public Task<string> ExtractTextFromPptxAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var result = new StringBuilder();

            for (var i = 1; i <= 100; i++)
            {
                var slideEntry = zip.GetEntry($"ppt/slides/slide{i}.xml");
                if (slideEntry is null) break;

                using var stream = slideEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var xml = reader.ReadToEnd();

                foreach (Match match in AtTagRegex().Matches(xml))
                {
                    result.Append(match.Groups[1].Value);
                    result.Append(' ');
                }
                result.AppendLine();
            }

            var text = result.ToString().Trim();
            return Task.FromResult(!string.IsNullOrWhiteSpace(text) ? text : $"[PPTX file: {Path.GetFileName(filePath)}]");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PPTX extraction failed for {Path}", filePath);
            return Task.FromResult($"[PPTX file: {Path.GetFileName(filePath)}]");
        }
    }

    public Task<List<string>> ChunkTextAsync(string text, int chunkSize = 1000, int overlap = 200, CancellationToken ct = default)
    {
        var chunks = new List<string>();
        if (string.IsNullOrEmpty(text))
            return Task.FromResult(chunks);

        if (text.Length <= chunkSize)
        {
            chunks.Add(text);
            return Task.FromResult(chunks);
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);
            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOf(' ', end - 1, Math.Min(chunkSize, end));
                if (lastSpace > start)
                    end = lastSpace;
            }

            chunks.Add(text[start..end]);
            start = end - overlap;
            if (start >= text.Length || start >= end)
                break;
        }

        return Task.FromResult(chunks);
    }

    private static string ExtractFromPdfCore(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var raw = Encoding.Latin1.GetString(bytes);
        var result = new StringBuilder();

        var streamRegex = PdfStreamRegex();
        foreach (Match streamMatch in streamRegex.Matches(raw))
        {
            var content = streamMatch.Groups[1].Value;
            foreach (Match textMatch in PdfTextOperatorRegex().Matches(content))
            {
                var textContent = textMatch.Groups[1].Value
                    .Replace("\\(", "(")
                    .Replace("\\)", ")")
                    .Replace("\\\\", "\\");

                if (!string.IsNullOrWhiteSpace(textContent) && textContent.Length > 1)
                    result.Append(textContent).Append(' ');
            }
        }

        if (result.Length == 0)
        {
            foreach (Match match in PdfParenTextRegex().Matches(raw))
            {
                var content = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(content) && content.Length > 2 &&
                    !content.Contains('\0') && PdfReadableTextRegex().IsMatch(content))
                    result.Append(content).Append(' ');
            }
        }

        var output = result.ToString().Trim();
        return !string.IsNullOrWhiteSpace(output) ? output : $"[PDF: {Path.GetFileName(filePath)}, {bytes.Length} bytes]";
    }

    [GeneratedRegex(@"<w:t[^>]*>([^<]+)</w:t>")]
    private static partial Regex WtTagRegex();

    [GeneratedRegex(@"<si><t>([^<]+)</t></si>")]
    private static partial Regex SiTTagRegex();

    [GeneratedRegex(@"<c[^>]*(?:t=""(\w)"")?[^>]*><v>([^<]+)</v></c>")]
    private static partial Regex CellValueRegex();

    [GeneratedRegex(@"<a:t>([^<]+)</a:t>")]
    private static partial Regex AtTagRegex();

    [GeneratedRegex(@"stream\s*\n(.*?)\nendstream", RegexOptions.Singleline)]
    private static partial Regex PdfStreamRegex();

    [GeneratedRegex(@"\(([^)\\]*(?:\\.[^)\\]*)*)\)\s*Tj")]
    private static partial Regex PdfTextOperatorRegex();

    [GeneratedRegex(@"\(([^)\\]*(?:\\.[^)\\]*)*)\)")]
    private static partial Regex PdfParenTextRegex();

    [GeneratedRegex(@"[a-zA-Z]{2,}")]
    private static partial Regex PdfReadableTextRegex();
}
