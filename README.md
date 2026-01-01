# AnyAPI

[![CI](https://github.com/Parslee-ai/anyapi/actions/workflows/ci.yml/badge.svg)](https://github.com/Parslee-ai/anyapi/actions/workflows/ci.yml)

A dynamic MCP (Model Context Protocol) server that exposes REST APIs as MCP tools. Register any API with an OpenAPI specification and let AI agents call it through a unified interface.

## Features

- **Dynamic API Registration** - Register APIs via OpenAPI 3.x, Swagger 2.0, GraphQL introspection, or Postman Collections
- **Multiple Auth Methods** - API Key, Bearer Token, Basic Auth, OAuth2 Client Credentials
- **Blazor Web UI** - Manage APIs, configure authentication, enable/disable endpoints
- **MCP Protocol** - Expose registered APIs as tools for AI agents (Claude, etc.)
- **Azure Native** - Cosmos DB storage, Key Vault for secrets, Container Apps deployment

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Azure Cosmos DB account (or emulator)
- Azure Key Vault (optional, for secret storage)

### Local Development

1. Clone the repository:
   ```bash
   git clone https://github.com/Parslee-ai/anyapi.git
   cd anyapi
   ```

2. Configure settings in `src/AnyAPI.Web/appsettings.json`:
   ```json
   {
     "Cosmos": {
       "ConnectionString": "your-cosmos-connection-string",
       "DatabaseName": "AnyApiDb"
     },
     "KeyVault": {
       "VaultUri": "https://your-keyvault.vault.azure.net/"
     }
   }
   ```

3. Run the web application:
   ```bash
   dotnet run --project src/AnyAPI.Web
   ```

4. Open https://localhost:5001 to access the management UI

### Running the MCP Server

```bash
dotnet run --project src/AnyAPI.Mcp
```

Configure your MCP client to connect to the server's stdio interface.

## Architecture

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   AnyAPI.Web    │     │   AnyAPI.Mcp    │     │  AnyAPI.Core    │
│  (Blazor UI)    │     │  (MCP Server)   │     │   (Shared)      │
│                 │     │                 │     │                 │
│ • Register APIs │     │ • Tool Provider │     │ • OpenAPI Parse │
│ • Configure Auth│     │ • Execute Calls │     │ • Auth Handlers │
│ • Enable/Disable│     │ • MCP Protocol  │     │ • Cosmos Store  │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┴───────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │      Azure Cosmos DB    │
                    │  • api-registrations    │
                    │  • api-endpoints        │
                    └─────────────────────────┘
```

### Project Structure

- **AnyAPI.Core** - Domain models, OpenAPI parsing, storage interfaces, auth handlers
- **AnyAPI.Web** - Blazor Server UI for API management
- **AnyAPI.Mcp** - MCP server exposing registered APIs as tools

## Supported API Formats

| Format | Support |
|--------|---------|
| OpenAPI 3.0/3.1 | Full |
| Swagger 2.0 | Full |
| GraphQL | Introspection-based |
| Postman Collection v2.1 | Full |

## Authentication

AnyAPI supports multiple authentication methods:

- **No Auth** - Public APIs
- **API Key** - Header, query, or cookie-based
- **Bearer Token** - JWT or opaque tokens
- **Basic Auth** - Username/password
- **OAuth2** - Client credentials flow

Secrets are stored securely in Azure Key Vault and referenced by name.

## Deployment

### Docker

```bash
docker build -t anyapi-web .
docker run -p 8080:8080 \
  -e Cosmos__ConnectionString="your-connection-string" \
  anyapi-web
```

### Azure Container Apps

```bash
# Build and push to ACR
az acr build --registry <registry> --resource-group <rg> \
  --image anyapi-web:v1 --file Dockerfile .

# Deploy to Container Apps
az containerapp create --name anyapi-web --resource-group <rg> \
  --image <registry>.azurecr.io/anyapi-web:v1 \
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
