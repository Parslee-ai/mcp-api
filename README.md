# MCP-API

[![CI](https://github.com/Parslee-ai/mcp-api/actions/workflows/ci.yml/badge.svg)](https://github.com/Parslee-ai/mcp-api/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Parslee-ai/mcp-api/graph/badge.svg)](https://codecov.io/gh/Parslee-ai/mcp-api)

A multi-tenant SaaS platform that turns REST APIs into MCP (Model Context Protocol) tools. Register any API with an OpenAPI specification and let AI agents call it through a unified interface.

## Features

- **Multi-Tenant Architecture** - Per-user API isolation with secure authentication
- **Dynamic API Registration** - Register APIs via OpenAPI 3.x, Swagger 2.0, GraphQL introspection, or Postman Collections
- **Multiple Auth Methods** - API Key, Bearer Token, Basic Auth, OAuth2 Client Credentials
- **Usage Tracking** - Tier-based limits with monthly quotas (Free, Pro, Enterprise)
- **Secure Secrets** - Per-user AES-256-GCM encryption with master key in Azure Key Vault
- **Token-Based MCP Auth** - Secure API tokens for MCP server authentication
- **Modern Web UI** - Next.js dashboard with shadcn/ui components
- **Azure Native** - Cosmos DB storage, Key Vault for secrets, Container Apps deployment

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Node.js 20+ (for frontend)
- Azure Cosmos DB account (or emulator)
- Azure Key Vault (optional, for secret storage)

### Local Development

1. Clone the repository:
   ```bash
   git clone https://github.com/Parslee-ai/mcp-api.git
   cd mcp-api
   ```

2. Configure API settings in `src/McpApi.Api/appsettings.json`:
   ```json
   {
     "Cosmos": {
       "ConnectionString": "your-cosmos-connection-string",
       "DatabaseName": "mcpapi"
     },
     "KeyVault": {
       "VaultUri": "https://your-keyvault.vault.azure.net/"
     },
     "Jwt": {
       "Secret": "your-256-bit-secret-key",
       "Issuer": "McpApi",
       "Audience": "McpApi"
     },
     "Cors": {
       "AllowedOrigins": ["http://localhost:3000"]
     }
   }
   ```

3. Run the API server:
   ```bash
   dotnet run --project src/McpApi.Api
   ```

4. Run the frontend (in a separate terminal):
   ```bash
   cd src/web
   npm install
   npm run dev
   ```

5. Open http://localhost:3000 to access the management UI

### Running the MCP Server

1. Create an API token in the web UI at `/tokens`
2. Set the token as an environment variable:
   ```bash
   export MCPAPI_TOKEN="mcp_your-token-here"
   export MCPAPI_COSMOS_CONNECTION_STRING="your-cosmos-connection-string"
   export MCPAPI_MASTER_KEY="your-encryption-master-key"
   ```
3. Run the MCP server:
   ```bash
   dotnet run --project src/McpApi.Mcp
   ```

Configure your MCP client to connect to the server's stdio interface. All API operations are scoped to your user account with tier-based limits.

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
                                └────────────┬────────────┘
                                             │
                                ┌────────────┴────────────┐
                                │      Azure Cosmos DB    │
                                │  • users                │
                                │  • api-registrations    │
                                │  • api-endpoints        │
                                │  • tokens               │
                                │  • usage                │
                                └─────────────────────────┘
```

### Project Structure

- **src/web** - Next.js 14+ frontend with App Router, shadcn/ui, and Tailwind CSS
- **McpApi.Api** - ASP.NET Core REST API with JWT authentication
- **McpApi.Core** - Domain models, OpenAPI parsing, storage, auth handlers, encryption
- **McpApi.Mcp** - MCP server with token-based auth exposing registered APIs as tools

## Supported API Formats

| Format | Support |
|--------|---------|
| OpenAPI 3.0/3.1 | Full |
| Swagger 2.0 | Full |
| GraphQL | Introspection-based |
| Postman Collection v2.1 | Full |

## Authentication

### User Authentication

MCP-API uses JWT-based authentication with access and refresh tokens:

1. **Register** - Create account with email and password
2. **Login** - Receive access token (15 min) and refresh token (7 days, httpOnly cookie)
3. **Verify Email** - Click verification link sent to your email
4. **Verify Phone** (optional) - Enter SMS code for additional security

### MCP Server Authentication

The MCP server uses token-based authentication:

1. **Create Token** - Generate an API token in the web UI at `/tokens`
2. **Set Environment Variable** - `export MCPAPI_TOKEN="mcp_your-token"`
3. **Run MCP Server** - Token is validated on startup

Tokens support optional expiration dates and can be revoked at any time.

### API Authentication (for registered APIs)

MCP-API supports multiple authentication methods for the APIs you register:

- **No Auth** - Public APIs
- **API Key** - Header, query, or cookie-based
- **Bearer Token** - JWT or opaque tokens
- **Basic Auth** - Username/password
- **OAuth2** - Client credentials flow

API secrets are encrypted per-user with AES-256-GCM. The master encryption key is stored in Azure Key Vault.

## Usage Tiers

| Feature | Free | Pro | Enterprise |
|---------|------|-----|------------|
| API Calls/Month | 1,000 | 50,000 | Unlimited |
| Registered APIs | 3 | 25 | Unlimited |
| Endpoints/API | 50 | 500 | Unlimited |

View your usage and remaining quota at `/usage` in the web UI.

## Deployment

### Docker

**API Server:**
```bash
docker build -t mcp-api .
docker run -p 8080:8080 \
  -e Cosmos__ConnectionString="your-connection-string" \
  -e Encryption__MasterKey="your-base64-encoded-32-byte-key" \
  -e KeyVault__VaultUri="https://your-keyvault.vault.azure.net/" \
  -e Jwt__Secret="your-jwt-secret" \
  mcp-api
```

**Frontend:**
```bash
cd src/web
docker build -t mcp-web \
  --build-arg NEXT_PUBLIC_API_URL=https://your-api-url/api .
docker run -p 3000:3000 mcp-web
```

### Azure Container Apps

```bash
# Build and push API to ACR
az acr build --registry <registry> --resource-group <rg> \
  --image mcp-api:v1 --file Dockerfile .

# Build and push frontend to ACR
az acr build --registry <registry> --resource-group <rg> \
  --image mcp-web:v1 --file src/web/Dockerfile \
  --build-arg NEXT_PUBLIC_API_URL=https://your-api.azurecontainerapps.io/api \
  src/web

# Deploy API to Container Apps
az containerapp create --name mcp-api --resource-group <rg> \
  --image <registry>.azurecr.io/mcp-api:v1 \
  --environment <env-name> \
  --ingress external --target-port 8080

# Deploy frontend to Container Apps
az containerapp create --name mcp-web --resource-group <rg> \
  --image <registry>.azurecr.io/mcp-web:v1 \
  --environment <env-name> \
  --ingress external --target-port 3000
```

## Development

### Build

```bash
# Backend
dotnet build

# Frontend
cd src/web && npm run build
```

### Test

```bash
dotnet test
```

### Run Specific Tests

```bash
dotnet test --filter "ClassName"
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## Security

See [SECURITY.md](SECURITY.md) for reporting vulnerabilities.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
