using R2WAI.Api.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace R2WAI.Api.Tests.Logging;

public class SensitiveDataMaskingTests
{
    private record LoginRequestLike(string Email, string Password, string? MfaCode = null);

    // Mirrors the real shapes that leaked in plaintext before the exact-name match in
    // SensitiveDataDestructuringPolicy was widened to a substring match: neither property name is
    // an exact match for "secret"/"token"/etc., only a longer name that embeds one of those terms.
    private record ApplicationApiRequestLike(string Name, string BaseUrl, string? CredentialSecret);
    private record WebhookKeyResultLike(string RawKey, string KeyPrefix);

    private sealed class CapturingSink : ILogEventSink
    {
        public readonly List<string> Rendered = new();
        public void Emit(LogEvent logEvent) => Rendered.Add(logEvent.RenderMessage());
    }

    [Fact]
    public void DestructuredObject_WithPasswordField_IsMaskedInLogOutput()
    {
        var sink = new CapturingSink();
        using var log = new LoggerConfiguration()
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var request = new LoginRequestLike("user@example.com", "SuperSecret123!");
        log.Information("Login attempt: {@Request}", request);

        var rendered = string.Concat(sink.Rendered);
        Assert.DoesNotContain("SuperSecret123!", rendered);
        Assert.Contains("***MASKED***", rendered);
        Assert.Contains("user@example.com", rendered);
    }

    [Fact]
    public void DestructuredObject_WithoutSensitiveFields_LogsNormally()
    {
        var sink = new CapturingSink();
        using var log = new LoggerConfiguration()
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var plain = new { Name = "Test Automation", Status = "Active" };
        log.Information("Created: {@Automation}", plain);

        var rendered = string.Concat(sink.Rendered);
        Assert.Contains("Test Automation", rendered);
    }

    [Fact]
    public void DestructuredObject_WithCredentialSecretField_IsMasked()
    {
        var sink = new CapturingSink();
        using var log = new LoggerConfiguration()
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var request = new ApplicationApiRequestLike("Billing API", "https://billing.internal", "Bearer sk-live-abc123");
        log.Information("Processing request: {@Request}", request);

        var rendered = string.Concat(sink.Rendered);
        Assert.DoesNotContain("sk-live-abc123", rendered);
        Assert.Contains("***MASKED***", rendered);
        Assert.Contains("Billing API", rendered);
    }

    [Fact]
    public void DestructuredObject_WithRawKeyField_IsMasked_ButKeyPrefixIsNot()
    {
        var sink = new CapturingSink();
        using var log = new LoggerConfiguration()
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var result = new WebhookKeyResultLike("r2w_abcdefghijklmnopqrstuvwxyz", "r2w_abcd");
        log.Information("Completed request: {@Response}", result);

        var rendered = string.Concat(sink.Rendered);
        Assert.DoesNotContain("r2w_abcdefghijklmnopqrstuvwxyz", rendered);
        Assert.Contains("***MASKED***", rendered);
        // KeyPrefix is the deliberately-displayable, non-secret 8-char prefix -- it must survive.
        Assert.Contains("r2w_abcd", rendered);
    }
}
