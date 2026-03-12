# Deployment Guide

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

MCP-API deploys to Azure Container Apps via Docker images pushed to Azure Container Registry (ACR). The CI/CD pipeline automates this on pushes to `main`.

## Infrastructure

| Component | Azure Service | Name |
|-----------|--------------|------|
| API server | Container Apps | `mcp-api` |
| Frontend | Container Apps | `mcp-web` |
| Database | Cosmos DB (NoSQL) | Database: `mcpapi` |
| Secrets | Key Vault | Master encryption key, connection strings |
| DNS | Azure DNS | Zone: `mcp-api.ai`, RG: `parslee-rg` |
| Container images | Container Registry | `mcp-api`, `mcp-web` |

## Docker Builds

### API Image

Multi-stage build using .NET 9 SDK:

```bash
docker build -t mcp-api .
```

The Dockerfile:
1. Restores NuGet packages
2. Builds `McpApi.Api` in Release mode
3. Publishes to `/app/publish`
4. Copies to `mcr.microsoft.com/dotnet/aspnet:9.0` runtime image
5. Exposes port 8080 (`ASPNETCORE_URLS=http://+:8080`)

### Frontend Image

```bash
docker build -t mcp-web \
  --build-arg NEXT_PUBLIC_API_URL=https://api.mcp-api.ai/api \
  src/web
```

`NEXT_PUBLIC_API_URL` is a build-time argument baked into the Next.js bundle.

## Manual Deployment

### Build and Push to ACR

```bash
# API
az acr build \
  --registry <registry> \
  --resource-group <rg> \
  --image mcp-api:v1 \
  --file Dockerfile .

# Frontend
az acr build \
  --registry <registry> \
  --resource-group <rg> \
  --image mcp-web:v1 \
  --file src/web/Dockerfile \
  --build-arg NEXT_PUBLIC_API_URL=https://api.mcp-api.ai/api \
  src/web
```

### Update Container Apps

```bash
# API
az containerapp update \
  --name mcp-api \
  --resource-group <rg> \
  --image <registry>.azurecr.io/mcp-api:v1

# Frontend
az containerapp update \
  --name mcp-web \
  --resource-group <rg> \
  --image <registry>.azurecr.io/mcp-web:v1
```

## CI/CD Automated Deployment

Pushes to `main` trigger the full pipeline (see [CI_CD_PIPELINE.md](CI_CD_PIPELINE.md)):

1. Build and test (.NET + Node.js)
2. Docker build and push to ACR (tagged with commit SHA + `latest`)
3. Deploy to Container Apps (requires `production` environment approval)

## DNS Configuration

Domain `mcp-api.ai` is managed in Azure DNS:

| Subdomain | Type | Target |
|-----------|------|--------|
| `api.mcp-api.ai` | CNAME | `mcp-api.politefield-aa1b1cd5.eastus2.azurecontainerapps.io` |
| `www.mcp-api.ai` | CNAME | `mcp-web.politefield-aa1b1cd5.eastus2.azurecontainerapps.io` |
| `mcp-api.ai` | A | `172.193.124.42` |

Nameservers are configured at Namecheap registrar pointing to Azure DNS.

## Environment Variables (Production)

### API Container App

Set via Container Apps configuration or Azure Key Vault references:

- `Cosmos:ConnectionString` - From Key Vault
- `Jwt:Secret` - From Key Vault
- `GitHub:ClientId` / `GitHub:ClientSecret` - App settings
- `Cors:AllowedOrigins` - `["https://mcp-api.ai", "https://www.mcp-api.ai"]`
- `App:FrontendUrl` - `https://mcp-api.ai`
- `KeyVault:VaultUri` - Key Vault URI
- `ASPNETCORE_ENVIRONMENT` - `Production`

### MCP Container (if deployed separately)

- `MCPAPI_TOKEN` - MCP authentication token
- `Cosmos:ConnectionString` - From Key Vault
- `KeyVault:VaultUri` - Key Vault URI

## See Also

- [CI_CD_PIPELINE.md](CI_CD_PIPELINE.md) - Automated CI/CD pipeline
- [CONFIGURATION.md](../reference/CONFIGURATION.md) - All configuration settings
- [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md) - Local development setup
