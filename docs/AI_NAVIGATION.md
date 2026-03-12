# AI Navigation Guide

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

Task-based routing for AI agents and developers working in the MCP-API codebase.

## I need to understand the system

| Task | Start Here |
|------|-----------|
| Understand the overall architecture | [SYSTEM_ARCHITECTURE.md](architecture/SYSTEM_ARCHITECTURE.md) |
| Understand how data is stored | [DATA_MODEL.md](architecture/DATA_MODEL.md) |
| Understand authentication and security | [SECURITY_MODEL.md](architecture/SECURITY_MODEL.md) |
| Understand subscription tiers and limits | [TIER_LIMITS.md](reference/TIER_LIMITS.md) |

## I need to work on the API

| Task | Start Here | Key Files |
|------|-----------|-----------|
| Add a new REST endpoint | [API_ENDPOINTS.md](reference/API_ENDPOINTS.md) | `src/McpApi.Api/Controllers/` |
| Modify authentication flow | [SECURITY_MODEL.md](architecture/SECURITY_MODEL.md) | `src/McpApi.Api/Auth/`, `src/McpApi.Api/Controllers/AuthController.cs` |
| Change API response format | [API_ENDPOINTS.md](reference/API_ENDPOINTS.md) | `src/McpApi.Api/DTOs/` |
| Add rate limiting rules | [CONFIGURATION.md](reference/CONFIGURATION.md) | `src/McpApi.Api/Program.cs` |

## I need to work on the MCP server

| Task | Start Here | Key Files |
|------|-----------|-----------|
| Add a new MCP tool | [MCP_TOOLS.md](reference/MCP_TOOLS.md) | `src/McpApi.Mcp/DynamicToolProvider.cs` |
| Change how API calls are executed | [MCP_TOOLS.md](reference/MCP_TOOLS.md) | `src/McpApi.Core/Http/DynamicApiClient.cs`, `src/McpApi.Core/Http/RequestBuilder.cs` |
| Modify token authentication | [SECURITY_MODEL.md](architecture/SECURITY_MODEL.md) | `src/McpApi.Core/Auth/McpTokenService.cs` |

## I need to work on API parsing

| Task | Start Here | Key Files |
|------|-----------|-----------|
| Fix OpenAPI parsing issues | [SPEC_PARSING_PIPELINE.md](patterns/SPEC_PARSING_PIPELINE.md) | `src/McpApi.Core/OpenApi/OpenApiParser.cs`, `OperationConverter.cs`, `SchemaFlattener.cs` |
| Add support for a new spec format | [SPEC_PARSING_PIPELINE.md](patterns/SPEC_PARSING_PIPELINE.md) | See existing parsers in `src/McpApi.Core/OpenApi/`, `GraphQL/`, `Postman/` |
| Add a new well-known API | [SPEC_PARSING_PIPELINE.md](patterns/SPEC_PARSING_PIPELINE.md) | `src/McpApi.Core/OpenApi/OpenApiDiscovery.cs` |

## I need to work on storage

| Task | Start Here | Key Files |
|------|-----------|-----------|
| Modify Cosmos DB queries | [DATA_MODEL.md](architecture/DATA_MODEL.md) | `src/McpApi.Core/Storage/CosmosApiRegistrationStore.cs` |
| Understand split storage | [SPLIT_STORAGE.md](patterns/SPLIT_STORAGE.md) | `src/McpApi.Core/Storage/CosmosApiRegistrationStore.cs` |
| Add a new Cosmos container | [DATA_MODEL.md](architecture/DATA_MODEL.md) | `src/McpApi.Core/Storage/CosmosContainerFactory.cs`, `Constants.cs` |

## I need to work on auth configuration

| Task | Start Here | Key Files |
|------|-----------|-----------|
| Add a new auth type | [POLYMORPHIC_AUTH.md](patterns/POLYMORPHIC_AUTH.md) | `src/McpApi.Core/Models/AuthConfiguration.cs`, `src/McpApi.Core/Auth/AuthHandlerFactory.cs` |
| Fix auth serialization | [POLYMORPHIC_AUTH.md](patterns/POLYMORPHIC_AUTH.md) | `AuthConfigurationConverter` in `AuthConfiguration.cs` |
| Modify secret encryption | [SECURITY_MODEL.md](architecture/SECURITY_MODEL.md) | `src/McpApi.Core/Secrets/AesGcmEncryptionService.cs` |

## I need to work on the frontend

| Task | Start Here | Key Files |
|------|-----------|-----------|
| Modify dashboard pages | [SYSTEM_ARCHITECTURE.md](architecture/SYSTEM_ARCHITECTURE.md) | `src/web/src/app/(dashboard)/` |
| Change API client calls | [API_ENDPOINTS.md](reference/API_ENDPOINTS.md) | `src/web/src/lib/api.ts`, `src/web/src/hooks/` |
| Modify authentication UI | [SECURITY_MODEL.md](architecture/SECURITY_MODEL.md) | `src/web/src/providers/auth-provider.tsx`, `src/web/src/app/auth/` |

## I need to deploy or configure

| Task | Start Here |
|------|-----------|
| Set up local development | [LOCAL_DEVELOPMENT.md](guides/LOCAL_DEVELOPMENT.md) |
| Deploy to Azure | [DEPLOYMENT.md](guides/DEPLOYMENT.md) |
| Understand CI/CD pipeline | [CI_CD_PIPELINE.md](guides/CI_CD_PIPELINE.md) |
| Change configuration settings | [CONFIGURATION.md](reference/CONFIGURATION.md) |

## See Also

- [INDEX.md](INDEX.md) - Full documentation index
