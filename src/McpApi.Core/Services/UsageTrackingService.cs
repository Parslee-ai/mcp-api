namespace McpApi.Core.Services;

using McpApi.Core.Models;
using McpApi.Core.Storage;

/// <summary>
/// Implementation of usage tracking with tier-based limits.
/// </summary>
public class UsageTrackingService : IUsageTrackingService
{
    private readonly IUsageStore _usageStore;
    private readonly IApiRegistrationStore _apiStore;

    public UsageTrackingService(IUsageStore usageStore, IApiRegistrationStore apiStore)
    {
        _usageStore = usageStore;
        _apiStore = apiStore;
    }

    public async Task<bool> CanMakeApiCallAsync(string userId, string userTier, CancellationToken ct = default)
    {
        var (maxCalls, _, _) = TierLimits.GetLimits(userTier);

        // Unlimited tier
        if (maxCalls == int.MaxValue)
            return true;

        var currentUsage = await _usageStore.GetCurrentMonthAsync(userId, ct);
        var currentCalls = currentUsage?.ApiCallCount ?? 0;

        return currentCalls < maxCalls;
    }

    public Task<UsageRecord> RecordApiCallAsync(string userId, CancellationToken ct = default)
    {
        return _usageStore.IncrementApiCallCountAsync(userId, ct);
    }

    public async Task<UsageRecord> CheckAndRecordApiCallAsync(string userId, string userTier, CancellationToken ct = default)
    {
        var (maxCalls, _, _) = TierLimits.GetLimits(userTier);

        // For unlimited tiers, just record without checking
        if (maxCalls == int.MaxValue)
        {
            return await RecordApiCallAsync(userId, ct);
        }

        var currentUsage = await _usageStore.GetCurrentMonthAsync(userId, ct);
        var currentCalls = currentUsage?.ApiCallCount ?? 0;

        if (currentCalls >= maxCalls)
        {
            throw new UsageLimitExceededException("API calls", currentCalls, maxCalls);
        }

        return await RecordApiCallAsync(userId, ct);
    }

    public bool CanRegisterApi(string userTier, int currentApiCount)
    {
        var (_, maxApis, _) = TierLimits.GetLimits(userTier);

        // Unlimited tier
        if (maxApis == int.MaxValue)
            return true;

        return currentApiCount < maxApis;
    }

    public async Task<int> GetRemainingApiCallsAsync(string userId, string userTier, CancellationToken ct = default)
    {
        var (maxCalls, _, _) = TierLimits.GetLimits(userTier);

        // Unlimited tier
        if (maxCalls == int.MaxValue)
            return int.MaxValue;

        var currentUsage = await _usageStore.GetCurrentMonthAsync(userId, ct);
        var currentCalls = currentUsage?.ApiCallCount ?? 0;

        return Math.Max(0, maxCalls - currentCalls);
    }

    public async Task<UsageSummary> GetUsageSummaryAsync(string userId, string userTier, CancellationToken ct = default)
    {
        var (maxCalls, maxApis, _) = TierLimits.GetLimits(userTier);
        var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");

        // Get current month usage
        var currentUsage = await _usageStore.GetCurrentMonthAsync(userId, ct);
        var apiCallsUsed = currentUsage?.ApiCallCount ?? 0;

        // Get current API count
        var apisRegistered = await _apiStore.GetApiCountAsync(userId, ct);

        // Calculate remaining (handle unlimited)
        var apiCallsRemaining = maxCalls == int.MaxValue ? int.MaxValue : Math.Max(0, maxCalls - apiCallsUsed);
        var apisRemaining = maxApis == int.MaxValue ? int.MaxValue : Math.Max(0, maxApis - apisRegistered);

        return new UsageSummary(
            ApiCallsUsed: apiCallsUsed,
            ApiCallsLimit: maxCalls,
            ApiCallsRemaining: apiCallsRemaining,
            ApisRegistered: apisRegistered,
            ApisLimit: maxApis,
            ApisRemaining: apisRemaining,
            Tier: userTier,
            YearMonth: yearMonth);
    }

    public async Task<IReadOnlyList<UsageRecord>> GetUsageHistoryAsync(string userId, int months = 12, CancellationToken ct = default)
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddMonths(-months + 1);

        var startYearMonth = startDate.ToString("yyyy-MM");
        var endYearMonth = endDate.ToString("yyyy-MM");

        return await _usageStore.GetRangeAsync(userId, startYearMonth, endYearMonth, ct);
    }
}
