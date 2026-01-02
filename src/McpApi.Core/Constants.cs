namespace McpApi.Core;

/// <summary>
/// Shared constants used across the McpApi.Core library.
/// </summary>
public static class Constants
{
    /// <summary>
    /// HTTP client names for IHttpClientFactory.
    /// </summary>
    public static class HttpClients
    {
        /// <summary>Name for the dynamic API client used for calling registered APIs.</summary>
        public const string DynamicApi = "DynamicApi";

        /// <summary>Name for the OAuth2 token client used for token requests.</summary>
        public const string OAuth2 = "OAuth2";
    }

    /// <summary>
    /// Cosmos DB configuration.
    /// </summary>
    public static class Cosmos
    {
        /// <summary>Container for API registration metadata.</summary>
        public const string ApiRegistrations = "api-registrations";

        /// <summary>Container for API endpoints (stored separately for large specs).</summary>
        public const string ApiEndpoints = "api-endpoints";

        /// <summary>Max concurrent Cosmos operations for batch upserts.</summary>
        public const int BatchConcurrency = 10;
    }

    /// <summary>
    /// HTTP headers and values.
    /// </summary>
    public static class Http
    {
        /// <summary>User-Agent header value for outgoing requests.</summary>
        public const string UserAgent = "McpApi/1.0";
    }

    /// <summary>
    /// OAuth2 authentication configuration.
    /// </summary>
    public static class OAuth2
    {
        /// <summary>Seconds before expiry to refresh token (prevents edge-case failures).</summary>
        public const int TokenRefreshBufferSeconds = 30;
    }

    /// <summary>
    /// OpenAPI schema processing configuration.
    /// </summary>
    public static class Schema
    {
        /// <summary>Maximum depth for flattening nested schemas (prevents infinite recursion).</summary>
        public const int MaxFlattenDepth = 10;
    }
}
