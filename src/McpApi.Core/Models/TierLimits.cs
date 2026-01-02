namespace McpApi.Core.Models;

/// <summary>
/// Defines usage limits for each subscription tier.
/// </summary>
public static class TierLimits
{
    /// <summary>
    /// Free tier limits.
    /// </summary>
    public static class Free
    {
        /// <summary>Maximum API calls per month.</summary>
        public const int MaxApiCallsPerMonth = 1000;

        /// <summary>Maximum number of registered APIs.</summary>
        public const int MaxApis = 3;

        /// <summary>Maximum endpoints per API.</summary>
        public const int MaxEndpointsPerApi = 50;
    }

    /// <summary>
    /// Pro tier limits.
    /// </summary>
    public static class Pro
    {
        /// <summary>Maximum API calls per month.</summary>
        public const int MaxApiCallsPerMonth = 50000;

        /// <summary>Maximum number of registered APIs.</summary>
        public const int MaxApis = 25;

        /// <summary>Maximum endpoints per API.</summary>
        public const int MaxEndpointsPerApi = 500;
    }

    /// <summary>
    /// Enterprise tier limits.
    /// </summary>
    public static class Enterprise
    {
        /// <summary>Maximum API calls per month (unlimited = int.MaxValue).</summary>
        public const int MaxApiCallsPerMonth = int.MaxValue;

        /// <summary>Maximum number of registered APIs (unlimited = int.MaxValue).</summary>
        public const int MaxApis = int.MaxValue;

        /// <summary>Maximum endpoints per API (unlimited = int.MaxValue).</summary>
        public const int MaxEndpointsPerApi = int.MaxValue;
    }

    /// <summary>
    /// Gets the limits for a specific tier.
    /// </summary>
    public static (int MaxApiCallsPerMonth, int MaxApis, int MaxEndpointsPerApi) GetLimits(string tier)
    {
        return tier.ToLowerInvariant() switch
        {
            "pro" => (Pro.MaxApiCallsPerMonth, Pro.MaxApis, Pro.MaxEndpointsPerApi),
            "enterprise" => (Enterprise.MaxApiCallsPerMonth, Enterprise.MaxApis, Enterprise.MaxEndpointsPerApi),
            _ => (Free.MaxApiCallsPerMonth, Free.MaxApis, Free.MaxEndpointsPerApi)
        };
    }
}
