# Configuration Reference

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

Configuration is managed via `appsettings.json`, environment variables, and Azure Key Vault. The API and MCP server share Core library configuration but have separate application-level settings.

## McpApi.Api Configuration

### Required Settings

| Setting | Source | Description |
|---------|--------|-------------|
| `Cosmos:ConnectionString` | Config or Key Vault (`cosmos-connection-string`) | Cosmos DB connection string |
| `Jwt:Secret` | Config or Key Vault (`jwt-signing-key`) | JWT signing key (min 32 characters, HMAC SHA-256) |
| `GitHub:ClientId` | Config | GitHub OAuth application client ID |
| `GitHub:ClientSecret` | Config | GitHub OAuth application client secret |

### Optional Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `Cosmos:DatabaseName` | `mcpapi` | Cosmos DB database name |
| `KeyVault:VaultUri` | (none) | Azure Key Vault URI for secret resolution |
| `Cors:AllowedOrigins` | (none) | Array of allowed frontend origins for CORS |
| `App:FrontendUrl` | `https://mcp-api.ai` | Frontend URL for OAuth redirect after login |
| `Jwt:Issuer` | `McpApi` | JWT token issuer claim |
| `Jwt:Audience` | `McpApi` | JWT token audience claim |

### Rate Limiting

Configured in `Program.cs` (not in appsettings):

| Policy | Limit | Window |
|--------|-------|--------|
| `fixed` (global) | 100 requests | 1 minute |
| `auth` | 10 requests | 1 minute |
| `demo` | 5 requests | 1 minute |

### JWT Token Lifetimes

| Token Type | Lifetime | Storage |
|------------|----------|---------|
| Access token | 15 minutes | Frontend memory |
| Refresh token | 7 days | httpOnly cookie + Cosmos DB |

## McpApi.Mcp Configuration

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `MCPAPI_TOKEN` | Yes (production) | MCP API token for authentication |
| `MCPAPI_USER_ID` | Dev only | User ID fallback for development (bypasses token auth) |

### appsettings.json

| Setting | Description |
|---------|-------------|
| `Cosmos:ConnectionString` | Cosmos DB connection string (shared with API) |
| `Cosmos:DatabaseName` | Cosmos DB database name (default: `mcpapi`) |
| `KeyVault:VaultUri` | Azure Key Vault URI for master encryption key |
| `Encryption:MasterKeySecretName` | Key Vault secret name for master encryption key |

## Frontend Configuration (src/web)

### Build-time Environment Variables

| Variable | Description |
|----------|-------------|
| `NEXT_PUBLIC_API_URL` | API base URL (e.g., `http://localhost:5001/api` for dev, `https://api.mcp-api.ai/api` for prod) |

This is a build-time variable baked into the Next.js bundle. Changing it requires a rebuild.

## Azure Key Vault Secrets

When `KeyVault:VaultUri` is configured, these secrets are resolved at startup:

| Secret Name | Used By | Description |
|-------------|---------|-------------|
| `cosmos-connection-string` | API + MCP | Cosmos DB connection string |
| `jwt-signing-key` | API | JWT HMAC SHA-256 signing key |
| Master encryption key | MCP | AES-256-GCM master key for HKDF derivation |

## GitHub OAuth Setup

1. Create OAuth App at `https://github.com/settings/developers`
2. Set callback URL to: `https://api.mcp-api.ai/api/auth/callback/github`
3. Configure `GitHub:ClientId` and `GitHub:ClientSecret` in API settings

## Azure DNS

Domain `mcp-api.ai` DNS is managed in Azure DNS zone:
- **Resource Group:** `parslee-rg`
- **Zone:** `mcp-api.ai`
- **API subdomain:** `api.mcp-api.ai` → Azure Container Apps CNAME
- **Web subdomain:** `www.mcp-api.ai` → Azure Container Apps CNAME

## See Also

- [LOCAL_DEVELOPMENT.md](../guides/LOCAL_DEVELOPMENT.md) - Local dev setup with configuration
- [DEPLOYMENT.md](../guides/DEPLOYMENT.md) - Production configuration for Azure
- [SECURITY_MODEL.md](../architecture/SECURITY_MODEL.md) - Security-related configuration details
