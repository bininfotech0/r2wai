using System.Net;

namespace R2WAI.Infrastructure.Security;

/// <summary>
/// Blocks outbound tool calls from reaching internal/private network addresses — the SSRF guard
/// flagged as P0-8 in the 2026-09-20 audit. Extracted from IntegrationsController's private
/// IsAllowedTestEndpoint (which only ever gated the "Test Connection" button) so the exact same rule
/// also gates real dispatch — <see cref="R2WAI.Infrastructure.AI.DynamicTools.DynamicToolExecutor"/>,
/// the path every AI-invoked and approval-resumed tool call actually goes through. Before this, an
/// AI (or a deferred call resumed after approval) could reach an internal address — including cloud
/// metadata endpoints like 169.254.169.254 — via a governed tool with no guard on the real call at
/// all, only on the separate test button.
///
/// IPv4 private-range detection is byte-range checks (RFC 1918 + link-local), carried over as-is from
/// the original method. IPv6 uses IPAddress's own IsIPv6LinkLocal/IsIPv6UniqueLocal/IsLoopback rather
/// than a literal string match against "::1" — the original method's string check silently never
/// matched in practice because Uri.Host keeps the brackets on an IPv6 literal ("[::1]", not "::1"),
/// so "http://[::1]" was never actually blocked despite looking like it should be; caught by this
/// class's own test suite, not by the original code ever having IPv6 coverage.
///
/// Default-deny for private/internal ranges, no configuration override today — same behavior the
/// Test button already shipped with, just applied consistently rather than newly invented. A real
/// on-premises deployment whose legitimate integration targets are internal addresses is a genuine,
/// still-open follow-on (an allowlist, e.g. Egress:AllowedNetworks) — not solved here; flagging
/// rather than guessing at the right shape for it.
/// </summary>
public static class EgressGuard
{
    public static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("https" or "http"))
            return false;

        // Uri.Host keeps the brackets on an IPv6 literal ("[::1]") — strip them before any string or
        // IPAddress.TryParse comparison, otherwise every IPv6 check below silently never matches.
        var host = uri.Host.Trim('[', ']');

        if (host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1" or "::")
            return false;

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
                return false;

            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = ip.GetAddressBytes();
                if (bytes[0] == 10) return false;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                if (bytes[0] == 169 && bytes[1] == 254) return false; // link-local — includes cloud metadata endpoints
            }
            else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal || ip.IsIPv6SiteLocal)
                    return false;
            }
        }

        var blockedSuffixes = new[] { ".internal", ".local", ".corp", ".svc.cluster.local" };
        if (blockedSuffixes.Any(s => host.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }
}
