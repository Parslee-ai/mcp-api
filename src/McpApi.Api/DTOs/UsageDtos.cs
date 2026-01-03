using McpApi.Core.Models;
using McpApi.Core.Services;

namespace McpApi.Api.DTOs;

public record UsageSummaryDto(
    int ApiCallsUsed,
    int ApiCallsLimit,
    int ApiCallsRemaining,
    int ApisRegistered,
    int ApisLimit,
    int ApisRemaining,
    string Tier,
    string YearMonth
);

public record UsageRecordDto(
    string YearMonth,
    int ApiCallCount,
    DateTime? FirstCallAt,
    DateTime? LastCallAt
);

public static class UsageDtoExtensions
{
    public static UsageSummaryDto ToDto(this UsageSummary summary)
    {
        return new UsageSummaryDto(
            summary.ApiCallsUsed,
            summary.ApiCallsLimit,
            summary.ApiCallsRemaining,
            summary.ApisRegistered,
            summary.ApisLimit,
            summary.ApisRemaining,
            summary.Tier,
            summary.YearMonth
        );
    }

    public static UsageRecordDto ToDto(this UsageRecord record)
    {
        return new UsageRecordDto(
            record.YearMonth,
            record.ApiCallCount,
            record.FirstCallAt,
            record.LastCallAt
        );
    }
}
