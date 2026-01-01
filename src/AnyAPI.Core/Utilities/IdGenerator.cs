namespace AnyAPI.Core.Utilities;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Shared utilities for generating unique identifiers across parsers.
/// </summary>
public static partial class IdGenerator
{
    /// <summary>
    /// Computes a short hash of the input string for use as a unique suffix.
    /// Uses first 4 bytes (8 hex chars) of SHA256 hash.
    /// </summary>
    public static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    /// <summary>
    /// Converts a string to a URL-safe slug (lowercase alphanumeric with dashes).
    /// </summary>
    public static string ToSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var slug = NonAlphanumericRegex().Replace(input.ToLowerInvariant(), "-");
        return slug.Trim('-');
    }

    /// <summary>
    /// Generates a unique API ID from a name and source URL.
    /// </summary>
    /// <param name="name">The display name of the API.</param>
    /// <param name="sourceUrl">The source URL (spec URL or base URL) for uniqueness.</param>
    /// <param name="fallback">Fallback name if slug is empty.</param>
    public static string GenerateApiId(string name, string sourceUrl, string fallback = "api")
    {
        var slug = ToSlug(name);
        var baseName = string.IsNullOrEmpty(slug) ? fallback : slug;
        var urlHash = ComputeShortHash(sourceUrl);
        return $"{baseName}-{urlHash}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();
}
