namespace AnyAPI.Core.OpenApi;

/// <summary>
/// Auto-discovers OpenAPI spec URLs from common locations and well-known APIs.
/// </summary>
public class OpenApiDiscovery
{
    private readonly HttpClient _httpClient;

    private static readonly string[] CommonSpecPaths =
    [
        "/openapi.json",
        "/openapi.yaml",
        "/swagger.json",
        "/swagger.yaml",
        "/api-docs",
        "/v3/api-docs",
        "/.well-known/openapi.json",
        "/.well-known/openapi.yaml",
        "/api/openapi.json",
        "/api/swagger.json"
    ];

    /// <summary>
    /// Well-known API spec URLs for popular APIs that don't serve specs from standard locations.
    /// Maps base URL patterns to their OpenAPI spec URLs.
    /// </summary>
    private static readonly Dictionary<string, WellKnownApi> WellKnownApis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["api.github.com"] = new WellKnownApi(
            "GitHub REST API",
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json"),

        ["api.stripe.com"] = new WellKnownApi(
            "Stripe API",
            "https://raw.githubusercontent.com/stripe/openapi/master/openapi/spec3.json"),

        ["api.openai.com"] = new WellKnownApi(
            "OpenAI API",
            "https://raw.githubusercontent.com/openai/openai-openapi/master/openapi.yaml"),

        ["api.slack.com"] = new WellKnownApi(
            "Slack Web API",
            "https://raw.githubusercontent.com/slackapi/slack-api-specs/master/web-api/slack_web_openapi_v2.json"),

        ["api.twilio.com"] = new WellKnownApi(
            "Twilio API",
            "https://raw.githubusercontent.com/twilio/twilio-oai/main/spec/json/twilio_api_v2010.json"),

        ["graph.microsoft.com"] = new WellKnownApi(
            "Microsoft Graph API",
            "https://raw.githubusercontent.com/microsoftgraph/msgraph-metadata/master/openapi/v1.0/openapi.yaml"),

        ["api.spotify.com"] = new WellKnownApi(
            "Spotify Web API",
            "https://raw.githubusercontent.com/sonallux/spotify-web-api/main/fixed-spotify-open-api.yml"),

        ["discord.com/api"] = new WellKnownApi(
            "Discord API",
            "https://raw.githubusercontent.com/discord/discord-api-spec/main/specs/openapi.json"),

        ["api.notion.com"] = new WellKnownApi(
            "Notion API",
            "https://raw.githubusercontent.com/NotionX/notion-openapi/main/openapi.json"),

        ["api.cloudflare.com"] = new WellKnownApi(
            "Cloudflare API",
            "https://raw.githubusercontent.com/cloudflare/api-schemas/main/openapi.json"),
    };

    public OpenApiDiscovery(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Attempts to discover the OpenAPI spec URL for a given base URL.
    /// Checks well-known APIs first, then probes common spec paths.
    /// </summary>
    public async Task<string?> DiscoverAsync(string baseUrl, CancellationToken ct = default)
    {
        var normalizedBase = baseUrl.TrimEnd('/');

        // Check well-known APIs first
        var wellKnown = GetWellKnownSpec(normalizedBase);
        if (wellKnown != null)
        {
            // Verify the well-known URL is still valid
            if (await IsValidSpecAsync(wellKnown.SpecUrl, ct))
            {
                return wellKnown.SpecUrl;
            }
        }

        // Try common spec paths
        foreach (var path in CommonSpecPaths)
        {
            var specUrl = normalizedBase + path;
            if (await IsValidSpecAsync(specUrl, ct))
            {
                return specUrl;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the well-known spec info for a base URL if available.
    /// </summary>
    public static WellKnownApi? GetWellKnownSpec(string baseUrl)
    {
        var normalizedBase = baseUrl.TrimEnd('/');

        // Extract host from URL
        if (Uri.TryCreate(normalizedBase, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();

            // Check exact host match
            if (WellKnownApis.TryGetValue(host, out var api))
            {
                return api;
            }

            // Check host + path for APIs like discord.com/api
            var hostPath = $"{host}{uri.AbsolutePath}".TrimEnd('/');
            if (WellKnownApis.TryGetValue(hostPath, out api))
            {
                return api;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all well-known APIs for display in UI.
    /// </summary>
    public static IReadOnlyDictionary<string, WellKnownApi> GetAllWellKnownApis() => WellKnownApis;

    /// <summary>
    /// Gets common spec paths to try for a given base URL.
    /// </summary>
    public static IEnumerable<string> GetCommonSpecUrls(string baseUrl)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        return CommonSpecPaths.Select(p => normalizedBase + p);
    }

    private async Task<bool> IsValidSpecAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
                return false;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            return contentType.Contains("json") ||
                   contentType.Contains("yaml") ||
                   contentType.Contains("text");
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Represents a well-known API with its spec URL.
/// </summary>
public record WellKnownApi(string Name, string SpecUrl);
