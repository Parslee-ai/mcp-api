# Security Model

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

MCP-API has multiple security layers: OAuth authentication for users, JWT sessions for the web API, token-based auth for MCP connections, AES-256-GCM encryption for stored secrets, and SSRF protection for spec URL fetching.

## Authentication Layers

### Web API Authentication (JWT + GitHub OAuth)

Users authenticate via GitHub OAuth. No email/password authentication exists.

**Flow:**
1. Frontend redirects to `/api/auth/login/github`
2. GitHub OAuth callback at `/api/auth/callback/github/complete`
3. API issues JWT access token (15-min) + refresh token (7-day httpOnly cookie)
4. Frontend stores access token in memory (not localStorage)
5. Axios interceptor auto-refreshes on 401 via `/api/auth/refresh`

**Implementation:**
- `AuthController.cs` - OAuth flow endpoints
- `JwtTokenService.cs` - JWT generation/validation (HMAC SHA-256)
- `AuthService.cs` - OAuth login with user creation
- `auth-provider.tsx` - Frontend auth context with auto-refresh

**Security decisions:**
- Account linking is disabled — if an email exists with a different OAuth provider, login is rejected to prevent account takeover
- Race condition protection on signup: catches Cosmos 409 Conflict for concurrent first-login

### MCP Server Authentication (Token-based)

AI agents authenticate with MCP tokens set via the `MCPAPI_TOKEN` environment variable.

**Flow:**
1. User creates token via web dashboard → receives `mcp_{base64}` plaintext (shown once)
2. User configures MCP client with token in env var
3. MCP server validates token hash on connection
4. Token `lastUsedAt` updated on each validation

**Implementation:**
- `McpTokenService.cs` - Token creation (SHA-256 hashing), validation, revocation
- `CosmosMcpTokenStore.cs` - Token storage partitioned by userId
- `McpApi.Mcp/Program.cs` - Token validation on startup

## Secret Management

### Encryption Architecture

API credentials (API keys, bearer tokens, OAuth2 secrets) are encrypted at rest using AES-256-GCM with per-user key derivation.

```
Azure Key Vault
    │
    ▼ (master key)
HKDF(masterKey, userId + userSalt)
    │
    ▼ (derived key, unique per user)
AES-256-GCM encrypt/decrypt
    │
    ▼
Cosmos DB (encrypted ciphertext + IV + auth tag)
```

**Implementation:**
- `AesGcmEncryptionService.cs` - AES-256-GCM with HKDF key derivation
- `KeyVaultSecretProvider.cs` - Azure Key Vault with in-memory caching
- `SecretReference.cs` - Dual-mode secret storage (encrypted preferred, Key Vault legacy)

**Parameters:**
- Key size: 256 bits (AES-256)
- IV size: 12 bytes (GCM recommended)
- Auth tag: 16 bytes
- User salt: 32 bytes (generated at user creation)

### SecretReference Model

Secrets use a dual-mode storage model:

| Mode | Fields Used | Description |
|------|------------|-------------|
| Encrypted (preferred) | `EncryptedValue`, `Iv`, `AuthTag` | AES-256-GCM encrypted, stored in Cosmos DB |
| Key Vault (legacy) | `SecretName`, `Version`, `VaultUri` | Reference to Azure Key Vault secret |

## SSRF Protection

`UrlValidator.ValidateExternalUrl()` prevents Server-Side Request Forgery when fetching API specifications.

**Blocked targets:**
- Localhost and loopback: `127.0.0.0/8`, `::1`, `localhost`
- Private networks: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`
- Link-local: `169.254.0.0/16`, `fe80::/10`
- Cloud metadata: `169.254.169.254`, `metadata.google.internal`
- Reserved: `0.0.0.0/8`, `fc00::/7` (unique local IPv6)
- Domain suffixes: `.local`, `.internal`, `.localhost`
- Schemes: Only HTTP and HTTPS allowed

**Implementation:** `McpApi.Core/Validation/UrlValidator.cs`

## Security Headers

`SecurityHeadersMiddleware` adds headers to all responses:

| Header | Value | Purpose |
|--------|-------|---------|
| `X-Content-Type-Options` | `nosniff` | Prevents MIME sniffing |
| `X-Frame-Options` | `DENY` | Prevents clickjacking |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Controls referrer info |
| `Content-Security-Policy` | `default-src 'self'` | Restricts resource origins |
| `Strict-Transport-Security` | `max-age=31536000` | Forces HTTPS (production only) |

## Rate Limiting

Configured in `Program.cs` with three tiers:

| Policy | Limit | Scope |
|--------|-------|-------|
| Global | 100 req/min | Per IP |
| Auth | 10 req/min | Auth endpoints |
| Demo | 5 req/min | Demo endpoints |

## Multi-Tenant Isolation

All Cosmos DB queries are scoped by `userId` partition key. Users can only access their own:
- API registrations and endpoints
- MCP tokens
- Usage records
- Refresh tokens

## Auth Handler Types

When executing API calls on behalf of users, auth is applied via `AuthHandlerFactory`:

| Auth Type | Handler | How It Works |
|-----------|---------|-------------|
| None | `NoOpAuthHandler` | No authentication applied |
| API Key | `ApiKeyAuthHandler` | Key in header, query, or cookie |
| Bearer Token | `BearerTokenAuthHandler` | `Authorization: Bearer {token}` header |
| Basic | `BasicAuthHandler` | `Authorization: Basic {base64}` header |
| OAuth2 | `OAuth2AuthHandler` | Client credentials or authorization code flow with token caching |

OAuth2 handlers are cached in a `ConcurrentDictionary` keyed by token URL + client ID + user context to preserve token state across calls.

## See Also

- [POLYMORPHIC_AUTH.md](../patterns/POLYMORPHIC_AUTH.md) - Auth configuration serialization
- [CONFIGURATION.md](../reference/CONFIGURATION.md) - Security-related configuration settings
- [SYSTEM_ARCHITECTURE.md](SYSTEM_ARCHITECTURE.md) - Overall architecture
