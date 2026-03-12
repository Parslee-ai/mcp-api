# System Architecture

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

MCP-API is a three-tier system that converts REST API specifications into MCP (Model Context Protocol) tools. AI agents connect to the MCP server, which dynamically exposes registered APIs as callable tools.

## Architecture Diagram

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    src/web       │     │   McpApi.Api     │     │   McpApi.Mcp    │
│   (Next.js 14+) │────▶│  (ASP.NET Core)  │     │  (MCP Server)   │
│                  │     │                  │     │                  │
│ • shadcn/ui      │     │ • JWT Auth       │     │ • Token Auth     │
│ • React Query    │     │ • Controllers    │     │ • DynamicTools   │
│ • Tailwind CSS   │     │ • Rate Limiting  │     │ • Usage Tracking │
└─────────────────┘     └────────┬─────────┘     └────────┬─────────┘
                                 │                         │
                                 └───────────┬─────────────┘
                                             │
                                ┌────────────┴────────────┐
                                │      McpApi.Core         │
                                │                          │
                                │ • OpenAPI/GraphQL/       │
                                │   Postman Parsing        │
                                │ • Auth Handlers          │
                                │ • Cosmos DB Storage      │
                                │ • AES-256-GCM Encryption │
                                │ • Usage Tracking         │
                                └──────────────────────────┘
```

## Components

### McpApi.Api (REST API Server)

ASP.NET Core 9 REST API serving the web frontend. Handles user authentication (GitHub OAuth), API registration, token management, and usage tracking.

**Key responsibilities:**
- GitHub OAuth login with JWT session management (15-min access tokens, 7-day refresh tokens)
- CRUD operations for API registrations and MCP tokens
- Rate limiting: 100 req/min global, 10 req/min auth, 5 req/min demo
- Security headers (CSP, HSTS, X-Frame-Options, MIME sniffing protection)
- Health checks (Cosmos DB connectivity)
- Swagger/OpenAPI documentation

**Entry point:** `src/McpApi.Api/Program.cs`

### McpApi.Mcp (MCP Server)

Standalone MCP server that AI agents connect to via stdio transport. Exposes registered APIs as MCP tools that agents can discover and call.

**Key responsibilities:**
- Token-based authentication (MCP tokens with SHA-256 hashing)
- Dynamic tool generation from registered API endpoints
- API call execution with auth handler application
- Usage tracking and tier-based limit enforcement

**Entry point:** `src/McpApi.Mcp/Program.cs`
**Tool provider:** `src/McpApi.Mcp/DynamicToolProvider.cs`

### McpApi.Core (Shared Library)

Domain models, business logic, and infrastructure shared between the API and MCP server.

**Key areas:**
- `Models/` - Domain entities: ApiRegistration, ApiEndpoint, AuthConfiguration, McpToken, User, UsageRecord
- `OpenApi/` - OpenAPI spec parser with schema flattening and operation conversion
- `GraphQL/` - GraphQL schema parser (introspection and SDL)
- `Postman/` - Postman Collection v2.1 parser
- `Storage/` - Cosmos DB stores with split storage pattern
- `Auth/` - Auth handler factory (API key, bearer, basic, OAuth2) and MCP token service
- `Http/` - Dynamic API client and request builder
- `Secrets/` - AES-256-GCM encryption and Azure Key Vault integration
- `Services/` - Usage tracking with tier-based limits

### src/web (Frontend)

Next.js 14+ application with App Router, shadcn/ui components, and Tailwind CSS.

**Key areas:**
- `app/` - Pages: landing, auth (login/callback), dashboard (APIs, tokens, usage)
- `components/` - UI components (shadcn/ui), dashboard components, landing page sections
- `hooks/` - React Query hooks for APIs, tokens, and usage data
- `providers/` - Auth context provider with auto-refresh, React Query provider
- `lib/api.ts` - Axios client with 401 interceptor for token refresh

## Data Flow

### API Registration

```
User submits spec URL → ApisController.Register()
  → UrlValidator.ValidateExternalUrl() (SSRF check)
  → OpenApiParser / GraphQLSchemaParser / PostmanCollectionParser
  → ApiRegistration + ApiEndpoint[] models
  → CosmosApiRegistrationStore.UpsertAsync() (metadata)
  → CosmosApiRegistrationStore.SaveEndpointsAsync() (endpoints, parallel batch)
```

### MCP Tool Execution

```
AI Agent calls tool → DynamicToolProvider.CallApi()
  → UsageTrackingService.CheckAndRecordApiCallAsync() (tier limit check)
  → CosmosApiRegistrationStore.GetAsync() + GetEndpointAsync()
  → RequestBuilder.Build() (construct HTTP request)
  → AuthHandlerFactory.Create() → IAuthHandler.ApplyAsync() (add auth)
  → DynamicApiClient.ExecuteAsync() (send HTTP request)
  → Return JSON response to agent
```

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Next.js 14+, React, TypeScript, Tailwind CSS, shadcn/ui, React Query |
| REST API | ASP.NET Core 9, C#, JWT authentication |
| MCP Server | ASP.NET Core 9, C#, MCP SDK (stdio transport) |
| Database | Azure Cosmos DB (NoSQL) with System.Text.Json serialization |
| Secrets | Azure Key Vault + AES-256-GCM encryption (HKDF key derivation) |
| CI/CD | GitHub Actions, Docker, Azure Container Registry, Azure Container Apps |
| Hosting | Azure Container Apps (API + Web), domain: mcp-api.ai |

## See Also

- [DATA_MODEL.md](DATA_MODEL.md) - Cosmos DB data model details
- [SECURITY_MODEL.md](SECURITY_MODEL.md) - Authentication and encryption architecture
- [AI_NAVIGATION.md](../AI_NAVIGATION.md) - Task-based navigation
