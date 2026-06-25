using System.Diagnostics;
using System.Net.Mail;

namespace R2WAI.Infrastructure.Services.ToolFramework;

public sealed class EmailToolOptions
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromAddress { get; set; }
    public bool EnableSsl { get; set; } = true;
}

public sealed class EmailTool : ITool
{
    private readonly EmailToolOptions _options;
    private readonly ILogger<EmailTool> _logger;

    public string Name => "EmailTool";
    public string Description => "Sends emails via SMTP";

    public EmailTool(EmailToolOptions options, ILogger<EmailTool> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            context.Parameters.TryGetValue("to", out var toObj);
            context.Parameters.TryGetValue("subject", out var subjectObj);
            context.Parameters.TryGetValue("body", out var bodyObj);

            var to = toObj?.ToString() ?? throw new InvalidOperationException("'to' parameter is required");
            var subject = subjectObj?.ToString() ?? "No Subject";
            var body = bodyObj?.ToString() ?? "";
            var from = _options.FromAddress ?? "noreply@r2wai.com";

            _logger.LogInformation("EmailTool sending email to {To}: {Subject}", to, subject);

            using var message = new MailMessage(from, to, subject, body);
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = !string.IsNullOrEmpty(_options.SmtpUser)
                    ? new System.Net.NetworkCredential(_options.SmtpUser, _options.SmtpPassword)
                    : null,
                Timeout = 15000
            };

            await client.SendMailAsync(message, context.CancellationToken);

            sw.Stop();
            _logger.LogInformation("EmailTool successfully sent email to {To} in {Duration}ms", to, sw.ElapsedMilliseconds);

            return new ToolResult
            {
                Success = true,
                Data = $"Email sent to {to}",
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "EmailTool execution failed");
            return new ToolResult
            {
                Success = false,
                Error = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }
}
