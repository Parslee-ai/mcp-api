# Tier Limits Reference

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

MCP-API enforces usage limits based on subscription tiers. Limits are defined in `McpApi.Core/Models/TierLimits.cs` and enforced by `UsageTrackingService`.

## Tier Comparison

| Limit | Free | Pro | Enterprise |
|-------|------|-----|-----------|
| API calls per month | 1,000 | 50,000 | Unlimited |
| Registered APIs | 3 | 25 | Unlimited |
| Endpoints per API | 50 | 500 | Unlimited |

"Unlimited" is implemented as `int.MaxValue` (2,147,483,647).

## Enforcement Points

### API Call Limits

Enforced in `DynamicToolProvider.CallApi()` before each API execution:

```
UsageTrackingService.CheckAndRecordApiCallAsync(userId, tier)
  → Checks current month's apiCallCount against tier limit
  → If under limit: increments count and proceeds
  → If at/over limit: throws UsageLimitExceededException
```

Usage is tracked monthly in the `usage` Cosmos container with document ID format `{userId}:{YYYY-MM}`.

### API Registration Limits

Enforced in `ApisController.Register()`:

```
UsageTrackingService.CanRegisterApi(tier, currentApiCount)
  → Returns false if currentApiCount >= tier's MaxApis
```

### Endpoint Limits

Enforced during API registration. If a parsed spec has more endpoints than the tier allows, only the first N endpoints are stored.

## Usage Tracking Methods

| Method | Description |
|--------|-------------|
| `CanMakeApiCallAsync(userId, tier)` | Check limit without recording |
| `RecordApiCallAsync(userId)` | Increment counter only |
| `CheckAndRecordApiCallAsync(userId, tier)` | Check and record atomically |
| `GetRemainingApiCallsAsync(userId, tier)` | Returns available calls |
| `GetUsageSummaryAsync(userId, tier)` | Full summary with counts and limits |
| `GetUsageHistoryAsync(userId, months)` | Monthly history for past N months |

## User Tier Assignment

User tier is stored in the `User.Tier` field (string: `"free"`, `"pro"`, `"enterprise"`). Default is `"free"`. The tier is included as a JWT claim for API requests and resolved from the user record for MCP connections.

## See Also

- [MCP_TOOLS.md](MCP_TOOLS.md) - GetUsage tool for checking limits
- [API_ENDPOINTS.md](API_ENDPOINTS.md) - Usage summary endpoint
- [DATA_MODEL.md](../architecture/DATA_MODEL.md) - UsageRecord document schema
