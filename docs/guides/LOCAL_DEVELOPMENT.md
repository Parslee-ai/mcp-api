# Local Development Guide

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Prerequisites

- .NET 9 SDK
- Node.js 20+
- Azure Cosmos DB Emulator (or Cosmos DB connection string)
- GitHub OAuth App (for authentication)

## Quick Start

### 1. Clone and Build

```bash
git clone <repo-url>
cd mcp-api
dotnet build
```

### 2. Configure API Server

Create or update `src/McpApi.Api/appsettings.Development.json`:

```json
{
  "Cosmos": {
    "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=<emulator-key>",
    "DatabaseName": "mcpapi"
  },
  "Jwt": {
    "Secret": "your-development-secret-key-at-least-32-chars-long",
    "Issuer": "McpApi",
    "Audience": "McpApi"
  },
  "GitHub": {
    "ClientId": "your-github-oauth-client-id",
    "ClientSecret": "your-github-oauth-client-secret"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  },
  "App": {
    "FrontendUrl": "http://localhost:3000"
  }
}
```

### 3. Set Up GitHub OAuth

1. Go to https://github.com/settings/developers
2. Create a new OAuth App
3. Set callback URL to: `http://localhost:5001/api/auth/callback/github`
4. Copy Client ID and Client Secret to config

### 4. Run API Server

```bash
dotnet run --project src/McpApi.Api
# API available at http://localhost:5001
# Swagger UI at http://localhost:5001/swagger
```

### 5. Run Frontend

```bash
cd src/web
npm install
NEXT_PUBLIC_API_URL=http://localhost:5001/api npm run dev
# Frontend available at http://localhost:3000
```

### 6. Run MCP Server (Optional)

```bash
# Set auth for development
export MCPAPI_USER_ID=your-user-id

dotnet run --project src/McpApi.Mcp
# MCP server uses stdio transport (stdin/stdout)
```

## Running Tests

```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "GitHubApiRegistrationTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure

```
mcp-api/
├── src/
│   ├── McpApi.Api/          # REST API server
│   ├── McpApi.Core/         # Shared domain library
│   ├── McpApi.Mcp/          # MCP server
│   └── web/                 # Next.js frontend
├── tests/
│   ├── McpApi.Core.Tests/   # Core library tests
│   └── McpApi.Mcp.Tests/    # MCP server tests
├── McpApi.sln               # Solution file
└── Dockerfile               # API Docker build
```

## Common Tasks

| Task | Command |
|------|---------|
| Build all | `dotnet build` |
| Build release | `dotnet build -c Release` |
| Run tests | `dotnet test` |
| Run API | `dotnet run --project src/McpApi.Api` |
| Run frontend | `cd src/web && npm run dev` |
| Build frontend | `cd src/web && npm run build` |
| Lint frontend | `cd src/web && npm run lint` |

## See Also

- [CONFIGURATION.md](../reference/CONFIGURATION.md) - Full configuration reference
- [DEPLOYMENT.md](DEPLOYMENT.md) - Deploying to Azure
- [CI_CD_PIPELINE.md](CI_CD_PIPELINE.md) - CI/CD pipeline details
