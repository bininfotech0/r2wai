using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using R2WAI.Infrastructure.Security;

namespace R2WAI.Infrastructure.Services.ToolFramework;

public sealed class HttpToolResilienceOptions
{
    public int RetryCount { get; set; } = 3;
    public Func<int, TimeSpan> RetryDelay { get; set; } = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt));
    public int BreakerFailureThreshold { get; set; } = 5;
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// The named HttpClient every tool call goes through.
///
/// Retry: the blanket "retry 3x on 5xx/408/network errors" that used to apply to every method re-sent
/// POST/PUT/PATCH/DELETE when the target had already applied the change but failed to answer — a
/// duplicate write against an enterprise system, with no idempotency key to make it harmless. Only
/// safe methods are retried now; unsafe methods run at most once per call.
///
/// Circuit breaker: one breaker per target host. A single shared breaker meant five failures against
/// any one host stopped every tenant's calls to every other host for 30 seconds.
/// </summary>
public static class HttpToolClient
{
    public const string Name = "HttpTool";

    public static SocketsHttpHandler CreateSecureHandler(IServiceProvider serviceProvider)
    {
        var egressOptions = serviceProvider.GetRequiredService<IOptions<EgressGuardOptions>>().Value;
        return new SocketsHttpHandler
        {
            // Redirect targets are untrusted and may point into private networks. Callers receive
            // the 3xx response and can choose a separately validated target.
            AllowAutoRedirect = false,
            // A proxy would move the socket destination away from the URL being validated.
            // Configure a trusted outbound proxy explicitly in a future proxy-aware policy.
            UseProxy = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (context, cancellationToken) =>
                EgressGuard.ConnectAsync(context, egressOptions, cancellationToken)
        };
    }

    public static bool IsSafeToRetry(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    public static IHttpClientBuilder AddHttpToolClient(
        this IServiceCollection services, Action<HttpToolResilienceOptions>? configure = null)
    {
        var options = new HttpToolResilienceOptions();
        configure?.Invoke(options);

        var breakersByHost = new ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase);

        return services
            .AddHttpClient(Name, client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "R2WAI-ToolFramework/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateSecureHandler)
            .AddPolicyHandler((_, request) => IsSafeToRetry(request.Method)
                ? HttpPolicyExtensions.HandleTransientHttpError()
                    .WaitAndRetryAsync(options.RetryCount, options.RetryDelay)
                : Policy.NoOpAsync<HttpResponseMessage>())
            .AddPolicyHandler((_, request) => breakersByHost.GetOrAdd(
                request.RequestUri?.Authority ?? "unknown",
                _ => HttpPolicyExtensions.HandleTransientHttpError()
                    .CircuitBreakerAsync(options.BreakerFailureThreshold, options.BreakDuration)));
    }
}
