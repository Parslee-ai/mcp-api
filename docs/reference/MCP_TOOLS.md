# MCP Tools Reference

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

The MCP server (`McpApi.Mcp`) exposes five tools via the `DynamicToolProvider` class. AI agents connect over stdio transport and authenticate with MCP tokens. All tool operations are scoped to the authenticated user.

**Implementation:** `src/McpApi.Mcp/DynamicToolProvider.cs`

## Tools

### ListAvailableApis

Lists all enabled API registrations for the authenticated user.

**Parameters:** None

**Returns:** JSON array of APIs with:
- `apiId` - API identifier
- `displayName` - Human-readable name
- `baseUrl` - API base URL
- `endpointCount` - Number of enabled endpoints
- `isEnabled` - Always `true` (only enabled APIs returned)

**Example response:**
```json
[
  {
    "apiId": "github-api",
    "displayName": "GitHub API",
    "baseUrl": "https://api.github.com",
    "endpointCount": 347,
    "isEnabled": true
  }
]
```

### CallApi

Executes an API call through a registered endpoint. Enforces usage limits before execution.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `apiId` | string | Yes | API registration ID |
| `operationId` | string | Yes | Endpoint operation ID |
| `parametersJson` | string | No | JSON object of parameter key-value pairs |

**Parameters JSON example:**
```json
{
  "owner": "anthropics",
  "repo": "claude-code",
  "state": "open",
  "per_page": 10
}
```

Parameters are mapped to their locations (path, query, header) based on the endpoint definition. Path parameters are URL-encoded. Remaining parameters not matching defined locations become the JSON request body.

**Returns:** JSON object with:
- `statusCode` - HTTP status code
- `headers` - Response headers
- `body` - Response body (parsed JSON or raw text)

**Error cases:**
- API not found or disabled → error message
- Endpoint not found or disabled → error message
- Usage limit exceeded → error with current usage and limit info
- HTTP error → status code and error body included

### GetApiDetails

Returns detailed information about a specific API and its endpoints.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `apiId` | string | Yes | API registration ID |

**Returns:** API metadata plus array of enabled endpoints with:
- `operationId`, `method`, `path`, `summary`
- `parameters` with types and descriptions
- `toolName` - The MCP tool name format

### SearchEndpoints

Full-text search across all enabled endpoints for the authenticated user.

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | string | Yes | Search term |

**Returns:** Matching endpoints with their API context. Searches across:
- Operation ID
- Path
- Summary

### GetUsage

Returns current month usage statistics and remaining quota.

**Parameters:** None

**Returns:** JSON object with:
- `tier` - User's subscription tier
- `currentMonth` - Year-month string
- `apiCallCount` - Calls made this month
- `maxApiCallsPerMonth` - Tier limit
- `remainingApiCalls` - Available calls

## Authentication

The MCP server authenticates via the `MCPAPI_TOKEN` environment variable:

```bash
# Production: Set MCP token
export MCPAPI_TOKEN=mcp_your_token_here

# Development: Use user ID directly
export MCPAPI_USER_ID=user-abc123
```

Token validation:
1. SHA-256 hash of plaintext token
2. Lookup hash in `tokens` Cosmos container
3. Check `isRevoked` and `expiresAt`
4. Update `lastUsedAt` timestamp
5. Resolve user's tier for usage limits

## Tool Name Format

Endpoints are exposed as MCP tools with names following the pattern:

```
{apiId}.{tag}.{operationId}
```

For example: `github-api.issues.list` or `stripe-api.customers.create`

Users can override tool names via `toolNameOverride` on individual endpoints.

## See Also

- [API_ENDPOINTS.md](API_ENDPOINTS.md) - REST API for managing registrations and tokens
- [TIER_LIMITS.md](TIER_LIMITS.md) - Usage limits by subscription tier
- [SECURITY_MODEL.md](../architecture/SECURITY_MODEL.md) - Token authentication details
