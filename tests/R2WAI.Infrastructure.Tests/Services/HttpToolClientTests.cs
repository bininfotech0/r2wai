using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using R2WAI.Infrastructure.Services.ToolFramework;

namespace R2WAI.Infrastructure.Tests.Services;

/// <summary>
/// Audit finding P0-2. The tool HTTP client retried every method 3x on 5xx/408/network errors — so a POST
/// that the target had already applied (but failed to answer) was sent again, a duplicate write against an
/// enterprise system — and one circuit breaker was shared by every tenant and every host.
/// </summary>
public class HttpToolClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public ConcurrentDictionary<string, int> CallsByHost { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Calls(string host) => CallsByHost.GetValueOrDefault(host);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            CallsByHost.AddOrUpdate(host, 1, (_, n) => n + 1);
            return Task.FromResult(new HttpResponseMessage(host == "bad.test" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
        }
    }

    private static (HttpClient Client, StubHandler Handler) Build()
    {
        var handler = new StubHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpToolClient(options => options.RetryDelay = _ => TimeSpan.Zero);
        services.AddHttpClient(HttpToolClient.Name).ConfigurePrimaryHttpMessageHandler(() => handler);
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient(HttpToolClient.Name);
        return (client, handler);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Unsafe_methods_are_sent_exactly_once_even_when_the_target_answers_5xx(string method)
    {
        var (client, handler) = Build();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "http://bad.test/supplier"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, handler.Calls("bad.test"));
    }

    [Fact]
    public async Task Safe_methods_are_still_retried()
    {
        var (client, handler) = Build();

        var response = await client.GetAsync("http://bad.test/supplier");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(4, handler.Calls("bad.test")); // the call + 3 retries
    }

    [Fact]
    public async Task A_failing_host_does_not_trip_the_breaker_for_other_hosts()
    {
        var (client, handler) = Build();

        // Enough consecutive failures against one host to open its breaker (threshold 5; each GET makes 4 attempts).
        var opened = false;
        for (var i = 0; i < 5 && !opened; i++)
        {
            try { await client.GetAsync("http://bad.test/supplier"); }
            catch (BrokenCircuitException) { opened = true; }
        }
        Assert.True(opened, "expected the bad host's circuit breaker to open");

        var good = await client.GetAsync("http://good.test/supplier");

        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(1, handler.Calls("good.test"));
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    [InlineData("DELETE", false)]
    public void Only_safe_methods_are_retryable(string method, bool expected) =>
        Assert.Equal(expected, HttpToolClient.IsSafeToRetry(new HttpMethod(method)));
}
