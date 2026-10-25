using System.Net;
using System.Net.Sockets;

namespace R2WAI.Infrastructure.Security;

/// <summary>
/// Configuration controlled by the deployment operator. Private network access remains denied by
/// default; exact hosts and CIDRs can be explicitly trusted for on-prem integrations.
/// </summary>
public sealed class EgressGuardOptions
{
    public List<string> AllowedHosts { get; set; } = [];
    public List<string> AllowedNetworks { get; set; } = [];
    public string? AllowedHostsCsv { get; set; }
    public string? AllowedNetworksCsv { get; set; }

    public IEnumerable<string> GetAllowedHosts() => AllowedHosts.Concat(SplitCsv(AllowedHostsCsv));
    public IEnumerable<string> GetAllowedNetworks() => AllowedNetworks.Concat(SplitCsv(AllowedNetworksCsv));

    private static IEnumerable<string> SplitCsv(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>
/// Validates outbound HTTP targets both before dispatch and against the resolved address used by
/// the socket. The connection-time check prevents DNS rebinding and validates every address a DNS
/// answer could select. Automatic redirects are disabled by the shared outbound client.
/// </summary>
public static class EgressGuard
{
    private static readonly string[] BlockedHostSuffixes =
        [".internal", ".local", ".corp", ".svc.cluster.local", ".localhost"];

    private static readonly string[] BlockedNetworks =
    [
        // IPv4: unspecified, private, shared address space, loopback, link-local, documentation,
        // protocol assignments, benchmarking, multicast and reserved ranges.
        "0.0.0.0/8", "10.0.0.0/8", "100.64.0.0/10", "127.0.0.0/8", "169.254.0.0/16",
        "172.16.0.0/12", "192.0.0.0/24", "192.0.2.0/24", "192.88.99.0/24", "192.168.0.0/16",
        "198.18.0.0/15", "198.51.100.0/24", "203.0.113.0/24", "224.0.0.0/4", "240.0.0.0/4",
        // IPv6: unspecified/loopback are also checked by IPAddress; the CIDRs cover local, special,
        // documentation, multicast and protocol-assignment ranges.
        "fc00::/7", "fe80::/10", "fec0::/10", "ff00::/8", "100::/64", "2001:2::/48",
        "2001:db8::/32", "2001::/23"
    ];

    public static bool IsAllowedUrl(string? url, EgressGuardOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        var host = NormalizeHost(uri.IdnHost);
        if (IsExplicitlyAllowedHost(host, options))
            return true;

        if (IPAddress.TryParse(host, out var address))
            return IsAllowedAddress(address, host, options);

        return !IsBlockedHost(host) || options?.GetAllowedNetworks().Any() == true;
    }

    /// <summary>Returns the origin only, so credentials, paths, and query tokens never enter logs.</summary>
    public static string SafeUrlForLog(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
            ? $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}"
            : "<invalid-url>";

    /// <summary>Checks the actual IP selected for a connection and the hostname's explicit trust.</summary>
    public static bool IsAllowedAddress(IPAddress address, string host, EgressGuardOptions? options = null)
    {
        host = NormalizeHost(host);
        if (IsExplicitlyAllowedHost(host, options))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IsBlockedHost(host) || IsInAnyNetwork(address, BlockedNetworks))
            return IsInAnyNetwork(address, options?.GetAllowedNetworks());

        return true;
    }

    /// <summary>
    /// Opens a socket only after resolving and validating every DNS result. The supplied hostname is
    /// retained by HttpClient for TLS SNI and certificate validation; only the TCP destination is
    /// pinned to the approved address from this resolution.
    /// </summary>
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        EgressGuardOptions options,
        CancellationToken cancellationToken)
    {
        var host = NormalizeHost(context.DnsEndPoint.Host);
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(address => !IsAllowedAddress(address, host, options)))
            throw new HttpRequestException("The outbound target resolved to a network address that is not allowed.");

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                socket.Dispose();
                lastError = ex;
            }
        }

        throw new HttpRequestException("Could not connect to the approved outbound target.", lastError);
    }

    private static bool IsBlockedHost(string host) =>
        host is "localhost" or "localhost.localdomain"
        || BlockedHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    private static bool IsExplicitlyAllowedHost(string host, EgressGuardOptions? options) =>
        options?.GetAllowedHosts().Any(allowed =>
            string.Equals(NormalizeHost(allowed), host, StringComparison.OrdinalIgnoreCase)) == true;

    private static string NormalizeHost(string host) => host.Trim().Trim('[', ']').TrimEnd('.').ToLowerInvariant();

    private static bool IsInAnyNetwork(IPAddress address, IEnumerable<string>? cidrs)
    {
        if (cidrs is null)
            return false;

        foreach (var cidr in cidrs)
        {
            if (TryParseCidr(cidr, out var network, out var prefixLength) && Contains(network, prefixLength, address))
                return true;
        }

        return false;
    }

    private static bool TryParseCidr(string cidr, out IPAddress network, out int prefixLength)
    {
        network = IPAddress.None;
        prefixLength = 0;
        var parts = cidr.Trim().Split('/', 2);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out network)
            || !int.TryParse(parts[1], out prefixLength))
            return false;

        if (network.IsIPv4MappedToIPv6)
            network = network.MapToIPv4();

        var maxPrefix = network.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        return prefixLength >= 0 && prefixLength <= maxPrefix;
    }

    private static bool Contains(IPAddress network, int prefixLength, IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (network.AddressFamily != address.AddressFamily)
            return false;

        var networkBytes = network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < wholeBytes; i++)
            if (networkBytes[i] != addressBytes[i])
                return false;

        if (remainingBits == 0)
            return true;

        var mask = (byte)(0xff << (8 - remainingBits));
        return (networkBytes[wholeBytes] & mask) == (addressBytes[wholeBytes] & mask);
    }
}
