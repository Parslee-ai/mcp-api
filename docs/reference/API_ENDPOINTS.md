# API Endpoints Reference

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

The REST API is served by `McpApi.Api` at `/api/*`. All endpoints except auth initiation and health require JWT authentication via `Authorization: Bearer {token}` header.

## Authentication Endpoints

**Controller:** `AuthController.cs`

### GitHub OAuth

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/auth/login/github` | No | Initiates GitHub OAuth flow. Optional `returnUrl` query param. |
| GET | `/api/auth/callback/github/complete` | No | GitHub OAuth callback. Issues JWT + refresh token cookie. Redirects to frontend. |

### Session Management

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/refresh` | No (cookie) | Refresh access token using httpOnly refresh token cookie. Returns new JWT. |
| POST | `/api/auth/logout` | Yes | Invalidates refresh token and clears cookie. |
| GET | `/api/auth/me` | Yes | Returns current user profile (id, email, displayName, avatarUrl, tier). |
| GET | `/api/auth/providers` | No | Lists available OAuth providers (currently GitHub). |

## API Registration Endpoints

**Controller:** `ApisController.cs` — All require authorization.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/apis` | List user's registered APIs with endpoint counts. |
| GET | `/api/apis/{id}` | Get API details including all endpoints. |
| POST | `/api/apis` | Register new API from OpenAPI/GraphQL/Postman spec. |
| PUT | `/api/apis/{id}` | Update API (display name, base URL, auth config). |
| DELETE | `/api/apis/{id}` | Delete API and all its endpoints. |
| PUT | `/api/apis/{id}/toggle` | Enable or disable an API. |
| POST | `/api/apis/{id}/refresh` | Re-parse spec URL and update endpoints. |
| GET | `/api/apis/{id}/endpoints` | List endpoints for an API. |
| PUT | `/api/apis/{id}/endpoints/{eid}/toggle` | Enable or disable a specific endpoint. |

### Register API Request

```json
POST /api/apis
{
  "specUrl": "https://api.example.com/openapi.json",
  "displayName": "My API",
  "auth": {
    "authType": "apiKey",
    "keyName": "X-API-Key",
    "keyLocation": "header",
    "apiKey": "sk-..."
  }
}
```

The `specUrl` field accepts:
- OpenAPI 3.x / Swagger 2.0 JSON or YAML URLs
- GraphQL endpoint URLs (auto-detected, introspected)
- Postman Collection v2.1 JSON URLs

If `specUrl` is omitted, the system attempts auto-discovery from `baseUrl` by probing common spec paths (`/openapi.json`, `/swagger.json`, `/api-docs`, etc.).

### Auth Configuration Types

| `authType` | Additional Fields |
|------------|------------------|
| `none` | (none) |
| `apiKey` | `keyName`, `keyLocation` (header/query/cookie), `apiKey` |
| `bearerToken` | `token` |
| `basic` | `username`, `password` |
| `oauth2` | `tokenUrl`, `clientId`, `clientSecret`, `scopes`, `grantType` |

## Token Endpoints

**Controller:** `TokensController.cs` — All require authorization.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/tokens` | List user's MCP tokens (hash omitted). |
| POST | `/api/tokens` | Create new token. Returns plaintext `mcp_*` token (shown once). |
| PUT | `/api/tokens/{id}/revoke` | Revoke a token (soft delete). |
| DELETE | `/api/tokens/{id}` | Permanently delete a token. |

### Create Token Request

```json
POST /api/tokens
{
  "name": "Claude Desktop",
  "expiresAt": "2026-06-12T00:00:00Z"
}
```

### Create Token Response

```json
{
  "id": "tok-abc123",
  "name": "Claude Desktop",
  "token": "mcp_dGhpcyBpcyBhIHRva2Vu...",
  "expiresAt": "2026-06-12T00:00:00Z",
  "createdAt": "2026-03-12T00:00:00Z"
}
```

The `token` field is only returned on creation. It cannot be retrieved again.

## Usage Endpoints

**Controller:** `UsageController.cs` — All require authorization.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/usage/summary` | Current month usage, limits, and remaining calls. |
| GET | `/api/usage/history?months=12` | Monthly usage history (default 12 months). |

### Usage Summary Response

```json
{
  "currentMonth": "2026-03",
  "apiCallCount": 42,
  "maxApiCallsPerMonth": 1000,
  "remainingApiCalls": 958,
  "tier": "free",
  "registeredApis": 2,
  "maxApis": 3
}
```

## Health Check

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | No | Cosmos DB health check. Returns 200 if healthy. |

## Error Responses

All errors return a consistent JSON format:

```json
{
  "error": "Error message description",
  "traceId": "00-abc123..."
}
```

The `GlobalExceptionMiddleware` catches unhandled exceptions and returns generic error messages (no stack traces in production).

## See Also

- [MCP_TOOLS.md](MCP_TOOLS.md) - MCP server tool reference
- [CONFIGURATION.md](CONFIGURATION.md) - API configuration settings
- [SECURITY_MODEL.md](../architecture/SECURITY_MODEL.md) - Authentication details
