# Data Model

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

MCP-API uses Azure Cosmos DB (NoSQL) with six containers. All containers use `userId` for multi-tenant isolation via partition keys. The `CosmosSystemTextJsonSerializer` replaces Newtonsoft.Json to support polymorphic type serialization (required for `AuthConfiguration`).

## Cosmos DB Containers

| Container | Partition Key | Purpose | Store Implementation |
|-----------|--------------|---------|---------------------|
| `api-registrations` | `/id` | API metadata (without endpoints) | `CosmosApiRegistrationStore` |
| `api-endpoints` | `/apiId` | Individual endpoint documents | `CosmosApiRegistrationStore` |
| `users` | `/id` | User accounts (OAuth-only) | `CosmosUserStore` |
| `tokens` | `/userId` | MCP API tokens (SHA-256 hashed) | `CosmosMcpTokenStore` |
| `usage` | `/userId` | Monthly usage records | `CosmosUsageStore` |
| `refresh-tokens` | `/userId` | JWT refresh tokens | `CosmosRefreshTokenStore` |

Container names are defined in `McpApi.Core/Constants.cs`.

## Document Schemas

### ApiRegistration

Stored in `api-registrations` container. The `endpoints` field is always empty in storage; endpoints are stored separately in `api-endpoints`.

```json
{
  "id": "github-api",
  "userId": "user-abc123",
  "displayName": "GitHub API",
  "baseUrl": "https://api.github.com",
  "specUrl": "https://raw.githubusercontent.com/.../api.github.com.json",
  "openApiVersion": "3.1.0",
  "auth": {
    "authType": "bearerToken",
    "token": {
      "encryptedValue": "base64...",
      "iv": "base64...",
      "authTag": "base64..."
    }
  },
  "endpoints": [],
  "isEnabled": true,
  "lastRefreshed": "2026-03-12T00:00:00Z",
  "createdAt": "2026-03-01T00:00:00Z",
  "_etag": "\"00000000-0000-0000-0000-000000000000\""
}
```

**Key fields:**
- `id` - Generated from API title and base URL
- `userId` - Owner's user ID (multi-tenant isolation)
- `auth` - Polymorphic `AuthConfiguration` with `authType` discriminator
- `endpoints` - Always `[]` in storage (see split storage pattern)
- `_etag` - Cosmos DB ETag for optimistic concurrency

### ApiEndpoint

Stored individually in `api-endpoints` container, partitioned by `apiId`.

```json
{
  "id": "get-repos-owner-repo-issues",
  "apiId": "github-api",
  "userId": "user-abc123",
  "operationId": "issues/list",
  "method": "GET",
  "path": "/repos/{owner}/{repo}/issues",
  "summary": "List repository issues",
  "description": "List issues in a repository.",
  "tags": ["issues"],
  "parameters": [
    {
      "name": "owner",
      "in": "path",
      "required": true,
      "description": "The account owner of the repository.",
      "schema": { "type": "string" }
    }
  ],
  "requestBody": null,
  "responses": { "200": { "description": "Success" } },
  "isEnabled": true,
  "toolNameOverride": null
}
```

**Key fields:**
- `apiId` - Partition key linking to parent ApiRegistration
- `operationId` - From OpenAPI spec or auto-generated
- `parameters` - Array of `ParameterDefinition` (path, query, header, cookie)
- `toolNameOverride` - Optional custom MCP tool name

### User

OAuth-only user accounts (no password storage).

```json
{
  "id": "user-abc123",
  "email": "user@example.com",
  "displayName": "Jane Developer",
  "avatarUrl": "https://avatars.githubusercontent.com/...",
  "oAuthProvider": "github",
  "oAuthProviderId": "12345",
  "emailVerified": true,
  "encryptionKeySalt": "base64-random-32-bytes",
  "tier": "free",
  "createdAt": "2026-03-01T00:00:00Z",
  "lastLoginAt": "2026-03-12T00:00:00Z"
}
```

### McpToken

MCP API tokens for authenticating MCP server connections.

```json
{
  "id": "tok-abc123",
  "userId": "user-abc123",
  "tokenHash": "sha256-hash-of-plaintext",
  "name": "My Claude Desktop Token",
  "expiresAt": "2026-06-12T00:00:00Z",
  "isRevoked": false,
  "createdAt": "2026-03-12T00:00:00Z",
  "lastUsedAt": "2026-03-12T10:30:00Z"
}
```

**Token format:** `mcp_{base64-url-safe-random}` — plaintext shown once at creation, only hash stored.

### UsageRecord

Monthly API call tracking per user.

```json
{
  "id": "user-abc123:2026-03",
  "userId": "user-abc123",
  "yearMonth": "2026-03",
  "apiCallCount": 42,
  "firstCallAt": "2026-03-01T08:00:00Z",
  "lastCallAt": "2026-03-12T10:30:00Z"
}
```

**ID format:** `{userId}:{YYYY-MM}` — one document per user per month.

### RefreshToken

JWT refresh tokens for web session management.

```json
{
  "id": "rt-abc123",
  "userId": "user-abc123",
  "tokenHash": "sha256-hash",
  "expiresAt": "2026-03-19T00:00:00Z",
  "isRevoked": false,
  "createdAt": "2026-03-12T00:00:00Z"
}
```

## Indexing Policy

The `api-registrations` container excludes `/endpoints/*` and `/_etag/?` from indexing to optimize write performance (endpoints are stored separately).

## Batch Operations

`SaveEndpointsAsync` uses `SemaphoreSlim` with `Constants.Cosmos.BatchConcurrency` (10) to throttle parallel upserts when saving large endpoint sets.

## See Also

- [SPLIT_STORAGE.md](../patterns/SPLIT_STORAGE.md) - Why endpoints are stored separately
- [POLYMORPHIC_AUTH.md](../patterns/POLYMORPHIC_AUTH.md) - AuthConfiguration serialization
- [SYSTEM_ARCHITECTURE.md](SYSTEM_ARCHITECTURE.md) - Overall architecture
