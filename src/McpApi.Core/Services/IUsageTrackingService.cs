namespace McpApi.Core.Services;

using McpApi.Core.Models;

/// <summary>
/// Service for tracking and enforcing usage limits.
/// </summary>
public interface IUsageTrackingService
{
    /// <summary>
    /// Checks if the user can make an API call based on their tier limits.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if allowed, false if limit exceeded.</returns>
    Task<bool> CanMakeApiCallAsync(string userId, string userTier, CancellationToken ct = default);

    /// <summary>
    /// Records an API call for the user.
    /// Should be called after a successful API call.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated usage record.</returns>
    Task<UsageRecord> RecordApiCallAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Checks and records an API call in one operation.
    /// Throws UsageLimitExceededException if limit is exceeded.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated usage record.</returns>
    Task<UsageRecord> CheckAndRecordApiCallAsync(string userId, string userTier, CancellationToken ct = default);

    /// <summary>
    /// Checks if the user can register a new API based on their tier limits.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="currentApiCount">Current number of registered APIs.</param>
    /// <returns>True if allowed, false if limit exceeded.</returns>
    bool CanRegisterApi(string userTier, int currentApiCount);

    /// <summary>
    /// Gets the remaining API calls for the current month.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Remaining calls, or int.MaxValue for unlimited.</returns>
    Task<int> GetRemainingApiCallsAsync(string userId, string userTier, CancellationToken ct = default);

    /// <summary>
    /// Gets the usage summary for the current month.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UsageSummary> GetUsageSummaryAsync(string userId, string userTier, CancellationToken ct = default);

    /// <summary>
    /// Gets usage history for the last N months.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="months">Number of months to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<UsageRecord>> GetUsageHistoryAsync(string userId, int months = 12, CancellationToken ct = default);
}

/// <summary>
/// Summary of a user's current usage and limits.
/// </summary>
public record UsageSummary(
    int ApiCallsUsed,
    int ApiCallsLimit,
    int ApiCallsRemaining,
    int ApisRegistered,
    int ApisLimit,
    int ApisRemaining,
    string Tier,
    string YearMonth);

/// <summary>
/// Exception thrown when a usage limit is exceeded.
/// </summary>
public class UsageLimitExceededException : Exception
{
    public string LimitType { get; }
    public int CurrentUsage { get; }
    public int Limit { get; }

    public UsageLimitExceededException(string limitType, int currentUsage, int limit)
        : base($"{limitType} limit exceeded: {currentUsage}/{limit}")
    {
        LimitType = limitType;
        CurrentUsage = currentUsage;
        Limit = limit;
    }
}
