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
- **Blazor Web UI** - Manage APIs, tokens, usage, and authentication
- **Azure Native** - Cosmos DB storage, Key Vault for secrets, Container Apps deployment

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Azure Cosmos DB account (or emulator)
- Azure Key Vault (optional, for secret storage)

### Local Development

1. Clone the repository:
   ```bash
   git clone https://github.com/Parslee-ai/mcp-api.git
   cd mcp-api
   ```

2. Configure settings in `src/McpApi.Web/appsettings.json`:
   ```json
   {
     "Cosmos": {
       "ConnectionString": "your-cosmos-connection-string",
       "DatabaseName": "mcpapi"
     },
     "KeyVault": {
       "VaultUri": "https://your-keyvault.vault.azure.net/"
     }
   }
   ```

3. Run the web application:
   ```bash
   dotnet run --project src/McpApi.Web
   ```

4. Open https://localhost:5001 to access the management UI

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
│   McpApi.Web    │     │   McpApi.Mcp    │     │  McpApi.Core    │
│  (Blazor UI)    │     │  (MCP Server)   │     │   (Shared)      │
│                 │     │                 │     │                 │
│ • User Auth     │     │ • Token Auth    │     │ • OpenAPI Parse │
│ • API Management│     │ • Tool Provider │     │ • Auth Handlers │
│ • Token Mgmt    │     │ • Execute Calls │     │ • Cosmos Store  │
│ • Usage Dashboard│    │ • Usage Tracking│     │ • Encryption    │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┴───────────────────────┘
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

- **McpApi.Core** - Domain models, OpenAPI parsing, storage, auth handlers, encryption
- **McpApi.Web** - Blazor Server UI for user auth, API management, tokens, and usage
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

MCP-API uses email-based authentication with optional phone verification:

1. **Register** - Create account with email and password
2. **Verify Email** - Click verification link sent to your email
3. **Verify Phone** (optional) - Enter SMS code for additional security

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

```bash
docker build -t mcpapi-web .
docker run -p 8080:8080 \
  -e Cosmos__ConnectionString="your-connection-string" \
  -e Encryption__MasterKey="your-base64-encoded-32-byte-key" \
  -e KeyVault__VaultUri="https://your-keyvault.vault.azure.net/" \
  mcpapi-web
```

### Azure Container Apps

```bash
# Build and push to ACR
az acr build --registry <registry> --resource-group <rg> \
  --image mcpapi-web:v1 --file Dockerfile .

# Deploy to Container Apps
az containerapp create --name mcpapi-web --resource-group <rg> \
  --image <registry>.azurecr.io/mcpapi-web:v1 \
  --environment <env-name> \
  --ingress external --target-port 8080
```

## Development

### Build

```bash
dotnet build
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
