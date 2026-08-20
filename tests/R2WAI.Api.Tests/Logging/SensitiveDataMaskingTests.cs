using R2WAI.Api.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace R2WAI.Api.Tests.Logging;

public class SensitiveDataMaskingTests
{
    private record LoginRequestLike(string Email, string Password, string? MfaCode = null);

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
}
