namespace McpApi.Core.Storage;

using McpApi.Core.Models;

/// <summary>
/// Repository interface for usage tracking records.
/// </summary>
public interface IUsageStore
{
    /// <summary>
    /// Gets the usage record for a user's specific month.
    /// Returns null if no usage recorded for that month.
    /// </summary>
    Task<UsageRecord?> GetAsync(string userId, string yearMonth, CancellationToken ct = default);

    /// <summary>
    /// Gets the usage record for a user's current month.
    /// Returns null if no usage recorded this month.
    /// </summary>
    Task<UsageRecord?> GetCurrentMonthAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Gets usage records for a user within a date range.
    /// </summary>
    Task<IReadOnlyList<UsageRecord>> GetRangeAsync(
        string userId,
        string startYearMonth,
        string endYearMonth,
        CancellationToken ct = default);

    /// <summary>
    /// Increments the API call count for the current month.
    /// Creates the record if it doesn't exist.
    /// Returns the updated record.
    /// </summary>
    Task<UsageRecord> IncrementApiCallCountAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a usage record.
    /// </summary>
    Task<UsageRecord> UpsertAsync(UsageRecord record, CancellationToken ct = default);
}
