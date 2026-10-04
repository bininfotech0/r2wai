using Microsoft.Extensions.Logging;
using Moq;
using R2WAI.Application.Common.Exceptions;
using R2WAI.Infrastructure.Integrations;

namespace R2WAI.Infrastructure.Tests.Integrations;

/// <summary>
/// OpenApiImportService.FetchSpecAsync had its own hand-rolled, IPv4-only SSRF check — a third,
/// never-consolidated copy of the same logic P0-8 already fixed once in IntegrationsController and
/// once in DynamicToolExecutor (see EgressGuard's own doc comment: Uri.Host keeps the brackets on an
/// IPv6 literal, so a literal string check against "::1" never matches "[::1]"). Now delegates to the
/// shared EgressGuard instead of a fourth divergent copy.
/// </summary>
public class OpenApiImportServiceTests
{
    private static OpenApiImportService CreateService()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        // Strict + no Setup: any call to CreateClient throws a Moq MockException — proving the
        // blocked URL never reached the point of even requesting an HttpClient to fetch it with.
        return new OpenApiImportService(httpClientFactory.Object, Mock.Of<ILogger<OpenApiImportService>>());
    }

    [Fact]
    public async Task AnalyzeAsync_IPv6Loopback_IsRejected_NeverReachesHttp()
    {
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.AnalyzeAsync("http://[::1]/openapi.json", null));

        Assert.Contains("url", ex.Errors.Keys);
        Assert.Contains("not allowed", ex.Errors["url"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_CloudMetadataEndpoint_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.AnalyzeAsync("http://169.254.169.254/latest/meta-data/", null));
    }

    [Fact]
    public async Task AnalyzeAsync_PrivateIPv4Range_IsRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.AnalyzeAsync("http://10.0.0.5/openapi.json", null));
    }

    [Fact]
    public async Task AnalyzeAsync_NeitherUrlNorFileContent_ThrowsValidation()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => service.AnalyzeAsync(null, null));
    }
}
