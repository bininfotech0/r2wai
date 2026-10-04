using R2WAI.Infrastructure.Security;

namespace R2WAI.Infrastructure.Tests.Security;

/// <summary>
/// P0-8 (2026-09-20 audit). This exact rule shipped once already as IntegrationsController's private
/// IsAllowedTestEndpoint (gating only the "Test Connection" button) — extracted here so
/// DynamicToolExecutor can enforce the same rule on real dispatch. These cases mirror what that
/// method already proved correct; nothing here is a new policy decision.
/// </summary>
public class EgressGuardTests
{
    [Theory]
    [InlineData("https://api.example.com")]
    [InlineData("http://api.example.com/v1/orders")]
    [InlineData("https://api.example.com:8443/path")]
    public void IsAllowedUrl_PublicHttpOrHttps_ReturnsTrue(string url)
    {
        Assert.True(EgressGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://api.example.com")]
    public void IsAllowedUrl_InvalidOrNonHttpScheme_ReturnsFalse(string? url)
    {
        Assert.False(EgressGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://[::1]")] // Uri.Host keeps the brackets here — the bug this regression-guards
    [InlineData("http://[::]")]
    public void IsAllowedUrl_Loopback_ReturnsFalse(string url)
    {
        Assert.False(EgressGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("http://[fe80::1]")] // link-local
    [InlineData("http://[fc00::1]")] // unique local (ULA)
    [InlineData("http://[fd12:3456:789a::1]")] // ULA
    public void IsAllowedUrl_PrivateIPv6Address_ReturnsFalse(string url)
    {
        Assert.False(EgressGuard.IsAllowedUrl(url));
    }

    [Fact]
    public void IsAllowedUrl_PublicIPv6Address_ReturnsTrue()
    {
        Assert.True(EgressGuard.IsAllowedUrl("http://[2606:4700:4700::1111]")); // a real public IPv6 address (Cloudflare)
    }

    [Theory]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://169.254.169.254")] // cloud metadata endpoint — the classic SSRF target
    public void IsAllowedUrl_PrivateOrLinkLocalIp_ReturnsFalse(string url)
    {
        Assert.False(EgressGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("http://172.15.255.255")] // just below the 172.16/12 block
    [InlineData("http://172.32.0.1")] // just above it
    [InlineData("http://8.8.8.8")]
    public void IsAllowedUrl_PublicIpJustOutsidePrivateRanges_ReturnsTrue(string url)
    {
        Assert.True(EgressGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("http://service.internal")]
    [InlineData("http://api.local")]
    [InlineData("http://erp.corp")]
    [InlineData("http://payments.svc.cluster.local")]
    public void IsAllowedUrl_BlockedInternalDnsSuffix_ReturnsFalse(string url)
    {
        Assert.False(EgressGuard.IsAllowedUrl(url));
    }
}
