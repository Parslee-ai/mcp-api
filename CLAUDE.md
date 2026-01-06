# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is MCP-API

MCP-API is a multi-tenant SaaS platform that turns REST APIs into MCP (Model Context Protocol) tools. It parses OpenAPI specifications and allows AI agents to call any registered API through a unified interface.

## Build Commands

```bash
# Build entire solution
dotnet build

# Build release
dotnet build -c Release

# Run tests
dotnet test

# Run specific test class
dotnet test --filter "GitHubApiRegistrationTests"

# Run API server locally
dotnet run --project src/McpApi.Api

# Run frontend locally
cd src/web && npm run dev

# Build frontend
cd src/web && npm run build

# Build Docker images
docker build -t mcp-api .
docker build -t mcp-web --build-arg NEXT_PUBLIC_API_URL=http://localhost:5001/api src/web
```

## Azure Deployment

```bash
# Build and push API to ACR
az acr build --registry <registry> --resource-group <rg> --image mcp-api:v1 --file Dockerfile .

# Build and push frontend to ACR
az acr build --registry <registry> --resource-group <rg> --image mcp-web:v1 \
  --file src/web/Dockerfile --build-arg NEXT_PUBLIC_API_URL=https://api.mcp-api.ai/api src/web

# Update API container app
az containerapp update --name mcp-api --resource-group <rg> --image <registry>.azurecr.io/mcp-api:v1

# Update frontend container app
az containerapp update --name mcp-web --resource-group <rg> --image <registry>.azurecr.io/mcp-web:v1
```

## Azure DNS Management

The domain `mcp-api.ai` DNS is managed in Azure DNS, enabling programmatic DNS changes via Azure CLI.

**Zone Details:**
- **Zone Name:** `mcp-api.ai`
- **Resource Group:** `parslee-rg`
- **Subscription:** Parslee Azure

**Current DNS Records:**

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | @ | 172.193.124.42 | 3600 |
| CNAME | www | mcp-web.politefield-aa1b1cd5.eastus2.azurecontainerapps.io | 3600 |
| MX | @ | eforward1-5.registrar-servers.com (10-20) | 3600 |
| TXT | @ | v=spf1 include:spf.efwd.registrar-servers.com ~all | 3600 |
| TXT | @ | _1gatgfdlovbnmrk5ykdxs99utehsi6q | 3600 |
| NS | @ | ns1-03.azure-dns.com, ns2-03.azure-dns.net, ns3-03.azure-dns.org, ns4-03.azure-dns.info | 172800 |

**Common DNS Operations:**

```bash
# List all records
az network dns record-set list \
  --resource-group parslee-rg \
  --zone-name mcp-api.ai \
  -o table

# Add A record
az network dns record-set a add-record \
  --resource-group parslee-rg \
  --zone-name mcp-api.ai \
  --record-set-name "subdomain" \
  --ipv4-address "1.2.3.4"

# Add CNAME record
az network dns record-set cname set-record \
  --resource-group parslee-rg \
  --zone-name mcp-api.ai \
  --record-set-name "api" \
  --cname "target.azurecontainerapps.io"

# Add TXT record (for domain verification)
az network dns record-set txt add-record \
  --resource-group parslee-rg \
  --zone-name mcp-api.ai \
  --record-set-name "@" \
  --value "verification-token"

# Delete a record set
az network dns record-set a delete \
  --resource-group parslee-rg \
  --zone-name mcp-api.ai \
  --name "subdomain" \
  --yes
```

**Nameservers (configured at Namecheap registrar):**
- ns1-03.azure-dns.com
- ns2-03.azure-dns.net
- ns3-03.azure-dns.org
- ns4-03.azure-dns.info

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    src/web      │     │   McpApi.Api    │     │   McpApi.Mcp    │
│   (Next.js)     │────▶│  (REST API)     │     │  (MCP Server)   │
│                 │     │                 │     │                 │
│ • shadcn/ui     │     │ • JWT Auth      │     │ • Token Auth    │
│ • React Query   │     │ • Controllers   │     │ • Tool Provider │
│ • Tailwind CSS  │     │ • CORS          │     │ • Execute Calls │
└─────────────────┘     └────────┬────────┘     └────────┬────────┘
                                 │                       │
                                 └───────────┬───────────┘
                                             │
                                ┌────────────┴────────────┐
                                │      McpApi.Core        │
                                │                         │
                                │ • OpenAPI/GraphQL Parse │
                                │ • Auth Handlers         │
                                │ • Cosmos Storage        │
                                │ • Encryption            │
                                └─────────────────────────┘
```

### Project Structure

- **src/web** - Next.js 14+ frontend with App Router, shadcn/ui, Tailwind CSS, React Query
- **McpApi.Api** - ASP.NET Core REST API with JWT authentication and CORS
- **McpApi.Core** - Domain models, OpenAPI parsing, storage, auth handlers, HTTP client
- **McpApi.Mcp** - MCP server that exposes registered APIs as tools

### Frontend Structure (src/web)

```
src/web/
├── src/
│   ├── app/                    # Next.js App Router pages
│   │   ├── page.tsx            # Landing page (public)
│   │   ├── auth/               # Login, Register, Verify pages
│   │   └── (dashboard)/        # Protected dashboard routes
│   │       ├── apis/           # API management
│   │       ├── tokens/         # MCP token management
│   │       └── usage/          # Usage statistics
│   ├── components/             # React components
│   │   ├── ui/                 # shadcn/ui components
│   │   ├── landing/            # Landing page components
│   │   └── dashboard/          # Dashboard components
│   ├── hooks/                  # Custom React hooks
│   ├── lib/                    # Utilities (api.ts, utils.ts)
│   └── providers/              # Context providers (Auth, Query)
├── Dockerfile                  # Production Docker build
└── next.config.ts              # Next.js configuration
```

### API Endpoints (McpApi.Api)

```
Auth (GitHub OAuth):
  GET    /api/auth/login/github   # Initiate GitHub OAuth
  GET    /api/auth/callback/github/complete # GitHub OAuth callback
  POST   /api/auth/refresh        # Refresh access token
  POST   /api/auth/logout         # Invalidate refresh token
  GET    /api/auth/me             # Current user
  GET    /api/auth/providers      # List available OAuth providers

APIs:
  GET    /api/apis                # List registered APIs
  GET    /api/apis/{id}           # Get API details
  POST   /api/apis                # Register new API
  PUT    /api/apis/{id}           # Update API
  DELETE /api/apis/{id}           # Delete API
  PUT    /api/apis/{id}/toggle    # Enable/disable API
  POST   /api/apis/{id}/refresh   # Re-parse spec
  GET    /api/apis/{id}/endpoints # List endpoints
  PUT    /api/apis/{id}/endpoints/{eid}/toggle

Tokens:
  GET    /api/tokens              # List MCP tokens
  POST   /api/tokens              # Create token
  PUT    /api/tokens/{id}/revoke  # Revoke token
  DELETE /api/tokens/{id}         # Delete token

Usage:
  GET    /api/usage/summary       # Current month usage
  GET    /api/usage/history       # Historical usage
```

### Data Flow

1. User registers API via Web UI with OpenAPI spec URL
2. `OpenApiParser` fetches and parses spec into `ApiRegistration` + `ApiEndpoint` models
3. `CosmosApiRegistrationStore` saves metadata to `api-registrations` container and endpoints to `api-endpoints` container (split storage for large APIs like GitHub with 900+ endpoints)
4. MCP clients call `DynamicToolProvider` which looks up endpoints and executes via `DynamicApiClient`

### Split Storage Pattern

Large APIs exceed Cosmos DB's 2MB document limit. The solution uses two containers:
- `api-registrations` (partition key: `/id`) - API metadata without endpoints
- `api-endpoints` (partition key: `/apiId`) - Individual endpoint documents

When saving: `UpsertAsync` clears endpoints from registration, `SaveEndpointsAsync` batch-upserts endpoints separately.

### OAuth Authentication

The API uses GitHub OAuth with JWT for session management:
- Users authenticate via GitHub OAuth (no email/password)
- After OAuth callback, API issues JWT access token + refresh token cookie
- **Access Token** - 15 min, stored in memory on frontend
- **Refresh Token** - 7 days, stored in httpOnly cookie
- Auto-refresh on 401 via axios interceptor in `src/web/src/lib/api.ts`
- New users are automatically created on first OAuth login

### Polymorphic Auth Configuration

`AuthConfiguration` is an abstract base with concrete types: `NoAuthConfig`, `ApiKeyAuthConfig`, `BearerTokenAuthConfig`, `BasicAuthConfig`, `OAuth2AuthConfig`.

Uses custom `AuthConfigurationConverter` for JSON serialization with `authType` discriminator. Falls back to `NoAuthConfig` if discriminator is missing (handles legacy data).

Cosmos DB requires `CosmosSystemTextJsonSerializer` to use System.Text.Json instead of Newtonsoft.Json for proper polymorphic support.

### Configuration

**API (McpApi.Api) - Required:**
- `Cosmos:ConnectionString` or `cosmos-connection-string` (Key Vault secret)
- `Jwt:Secret` or `jwt-signing-key` (Key Vault secret) - at least 32 characters

**API - GitHub OAuth (required):**
- `GitHub:ClientId` - GitHub OAuth client ID
- `GitHub:ClientSecret` - GitHub OAuth client secret

To set up GitHub OAuth:
1. Create OAuth App at https://github.com/settings/developers
2. Add callback URL: `https://api.mcp-api.ai/api/auth/callback/github`

**API - Optional:**
- `Cosmos:DatabaseName` (default: "mcpapi")
- `KeyVault:VaultUri` (optional, for secret references)
- `Cors:AllowedOrigins` (array of allowed frontend origins)
- `App:FrontendUrl` (default: "https://mcp-api.ai") - for OAuth redirect

**Frontend (src/web):**
- `NEXT_PUBLIC_API_URL` - API base URL (e.g., `http://localhost:5001/api`)

### Key Interfaces

- `IApiRegistrationStore` - CRUD for APIs and endpoints
- `IOpenApiParser` - Parse OpenAPI specs to models
- `IApiClient` - Execute HTTP calls with auth
- `IAuthHandler` - Apply authentication to requests
- `IJwtTokenService` - Generate/validate JWT tokens
- `IRefreshTokenStore` - Store refresh tokens in Cosmos DB
