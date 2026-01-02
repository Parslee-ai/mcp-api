# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is MCP-API

MCP-API is a dynamic MCP (Model Context Protocol) server that exposes REST APIs as MCP tools. It parses OpenAPI specifications and allows AI agents to call any registered API through a unified interface.

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

# Run web app locally
dotnet run --project src/McpApi.Web

# Build Docker image
docker build -t mcpapi-web .
```

## Azure Deployment

```bash
# Build and push to ACR
az acr build --registry <registry> --resource-group <rg> --image mcpapi-web:v1 --file Dockerfile .

# Update container app
az containerapp update --name mcpapi-web --resource-group <rg> --image <registry>.azurecr.io/mcpapi-web:v1
```

## Architecture

### Project Structure

- **McpApi.Core** - Domain models, OpenAPI parsing, storage, auth handlers, HTTP client
- **McpApi.Web** - Blazor Server UI for API management (register, configure, enable/disable)
- **McpApi.Mcp** - MCP server that exposes registered APIs as tools

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

### Polymorphic Auth Configuration

`AuthConfiguration` is an abstract base with concrete types: `NoAuthConfig`, `ApiKeyAuthConfig`, `BearerTokenAuthConfig`, `BasicAuthConfig`, `OAuth2AuthConfig`.

Uses custom `AuthConfigurationConverter` for JSON serialization with `authType` discriminator. Falls back to `NoAuthConfig` if discriminator is missing (handles legacy data).

Cosmos DB requires `CosmosSystemTextJsonSerializer` to use System.Text.Json instead of Newtonsoft.Json for proper polymorphic support.

### Configuration

Required settings (via appsettings.json or environment variables):
- `Cosmos:ConnectionString` or `cosmos-connection-string` (Key Vault secret)
- `Cosmos:DatabaseName` (default: "mcpapi")
- `KeyVault:VaultUri` (optional, for secret references)

Secrets in auth configs reference Azure Key Vault via `SecretReference.SecretName`.

### Key Interfaces

- `IApiRegistrationStore` - CRUD for APIs and endpoints
- `IOpenApiParser` - Parse OpenAPI specs to models
- `IApiClient` - Execute HTTP calls with auth
- `IAuthHandler` - Apply authentication to requests
