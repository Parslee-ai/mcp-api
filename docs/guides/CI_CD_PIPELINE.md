# CI/CD Pipeline

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

GitHub Actions CI/CD pipeline defined in `.github/workflows/ci.yml`. Runs on pushes to `main`, pull requests to `main`, and manual dispatch.

## Pipeline Stages

```
┌──────────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────┐
│  build-and-test  │     │  build-web   │     │ docker-build │     │  deploy  │
│  (.NET 9)        │────▶│  (Node 20)   │────▶│  (ACR push)  │────▶│ (Azure)  │
│                  │     │              │     │              │     │          │
│ • Restore        │     │ • npm ci     │     │ • API image  │     │ • API    │
│ • Build Release  │     │ • Lint       │     │ • Web image  │     │ • Web    │
│ • Vuln check     │     │ • Build      │     │ • SHA + latest│    │          │
│ • Test + coverage│     │              │     │              │     │          │
└──────────────────┘     └──────────────┘     └──────────────┘     └──────────┘
       (always)               (always)         (main push only)    (if docker ok)
```

## Job Details

### build-and-test

Runs on every push and PR.

1. Setup .NET 9 SDK
2. `dotnet restore`
3. `dotnet build --no-restore -c Release`
4. Check for vulnerable NuGet packages (warning, not blocking)
5. `dotnet test` with XPlat Code Coverage
6. Generate coverage report (Cobertura + Markdown)
7. Upload to Codecov
8. Post coverage as PR comment (sticky, recreated on updates)

### build-web

Runs on every push and PR.

1. Setup Node.js 20 with npm cache
2. `npm ci` (clean install from lock file)
3. `npm run lint`
4. `npm run build` with `NEXT_PUBLIC_API_URL` from repository variables

### docker-build

Runs only on `main` push when ACR secrets are configured.

**Conditions:** `github.ref == 'refs/heads/main' && github.event_name == 'push' && vars.ACR_LOGIN_SERVER != ''`

1. Setup Docker Buildx
2. Login to Azure Container Registry (username/password)
3. Build and push API image (`mcp-api:{sha}`, `mcp-api:latest`)
4. Build and push Web image (`mcp-web:{sha}`, `mcp-web:latest`)

### deploy

Runs only if `docker-build` succeeds. Requires `production` environment approval.

1. Azure login via OIDC (client ID, tenant ID, subscription ID)
2. `az containerapp update` for API with commit SHA tag
3. `az containerapp update` for Web with commit SHA tag

## Required Secrets and Variables

### Repository Secrets

| Secret | Used By | Description |
|--------|---------|-------------|
| `ACR_USERNAME` | docker-build | Azure Container Registry username |
| `ACR_PASSWORD` | docker-build | Azure Container Registry password |
| `AZURE_CLIENT_ID` | deploy | Azure OIDC client ID |
| `AZURE_TENANT_ID` | deploy | Azure OIDC tenant ID |
| `AZURE_SUBSCRIPTION_ID` | deploy | Azure subscription ID |
| `CODECOV_TOKEN` | build-and-test | Codecov upload token |

### Repository Variables

| Variable | Used By | Description |
|----------|---------|-------------|
| `ACR_LOGIN_SERVER` | docker-build | ACR server URL (e.g., `myregistry.azurecr.io`) |
| `NEXT_PUBLIC_API_URL` | build-web, docker-build | API URL for frontend build |
| `API_CONTAINER_APP_NAME` | deploy | Azure Container App name for API |
| `WEB_CONTAINER_APP_NAME` | deploy | Azure Container App name for frontend |
| `AZURE_RESOURCE_GROUP` | deploy | Azure resource group name |

## Additional Workflows

### Dependabot

Configured in `.github/dependabot.yml` for automated dependency updates.

### CodeQL

Security scanning via `.github/workflows/codeql.yml`.

## See Also

- [DEPLOYMENT.md](DEPLOYMENT.md) - Manual deployment procedures
- [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md) - Local development setup
