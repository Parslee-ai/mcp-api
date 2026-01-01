namespace AnyAPI.Core.Validation;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// Validates URLs to prevent SSRF attacks.
/// </summary>
public static class UrlValidator
{
    private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
        "0.0.0.0",
        "169.254.169.254", // AWS/Azure/GCP metadata
        "metadata.google.internal",
        "metadata.goog"
    };

    /// <summary>
    /// Validates that a URL is safe to fetch (not internal/metadata endpoints).
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <exception cref="ArgumentException">Thrown if the URL is invalid or blocked.</exception>
    public static void ValidateExternalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty.", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL format: {url}", nameof(url));

        // Only allow HTTP and HTTPS
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException($"URL scheme must be HTTP or HTTPS: {url}", nameof(url));

        var host = uri.Host;

        // Check against blocked hosts
        if (BlockedHosts.Contains(host))
            throw new ArgumentException($"URL host is not allowed: {host}", nameof(url));

        // Check for private/internal IP addresses
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            if (IsPrivateOrReserved(ipAddress))
                throw new ArgumentException($"URL points to a private or reserved IP address: {host}", nameof(url));
        }

        // Block link-local hostnames
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"URL host is not allowed: {host}", nameof(url));
        }
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();

        // IPv4
        if (ip.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127)
                return true;

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            // 0.0.0.0/8
            if (bytes[0] == 0)
                return true;
        }

        // IPv6
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Loopback (::1)
            if (IPAddress.IsLoopback(ip))
                return true;

            // Link-local (fe80::/10)
            if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
                return true;

            // Unique local (fc00::/7)
            if ((bytes[0] & 0xfe) == 0xfc)
                return true;
        }

        return false;
    }
}
