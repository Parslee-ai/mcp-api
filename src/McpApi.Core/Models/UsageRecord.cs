namespace McpApi.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Tracks monthly API usage for a user.
/// Stored in Cosmos DB with userId as partition key.
/// </summary>
public class UsageRecord
{
    /// <summary>
    /// Unique ID in format "{userId}:{yearMonth}" (e.g., "user123:2024-01").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Owner user ID (partition key for multi-tenancy).
    /// </summary>
    [JsonPropertyName("userId")]
    public required string UserId { get; set; }

    /// <summary>
    /// Year-month in format "YYYY-MM" (e.g., "2024-01").
    /// </summary>
    public required string YearMonth { get; set; }

    /// <summary>
    /// Number of API calls made this month.
    /// </summary>
    public int ApiCallCount { get; set; }

    /// <summary>
    /// When the first API call was made this month.
    /// </summary>
    public DateTime? FirstCallAt { get; set; }

    /// <summary>
    /// When the last API call was made.
    /// </summary>
    public DateTime? LastCallAt { get; set; }

    /// <summary>
    /// Creates a new usage record for the current month.
    /// </summary>
    public static UsageRecord CreateForCurrentMonth(string userId)
    {
        var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
        return new UsageRecord
        {
            Id = $"{userId}:{yearMonth}",
            UserId = userId,
            YearMonth = yearMonth,
            ApiCallCount = 0
        };
    }

    /// <summary>
    /// Creates the ID for a usage record.
    /// </summary>
    public static string CreateId(string userId, string yearMonth) => $"{userId}:{yearMonth}";
}
