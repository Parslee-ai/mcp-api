# MCP-API Documentation Index

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

MCP-API is a multi-tenant SaaS platform that turns REST APIs into MCP (Model Context Protocol) tools. It parses OpenAPI, GraphQL, and Postman specifications and allows AI agents to call any registered API through a unified interface.

## Architecture

| Document | Description |
|----------|-------------|
| [SYSTEM_ARCHITECTURE.md](architecture/SYSTEM_ARCHITECTURE.md) | Three-tier architecture: Next.js frontend, ASP.NET Core API, MCP server |
| [DATA_MODEL.md](architecture/DATA_MODEL.md) | Cosmos DB data model, containers, partition keys, and document schemas |
| [SECURITY_MODEL.md](architecture/SECURITY_MODEL.md) | Authentication, encryption, secret management, SSRF protection |

## Reference

| Document | Description |
|----------|-------------|
| [API_ENDPOINTS.md](reference/API_ENDPOINTS.md) | Complete REST API endpoint reference with request/response details |
| [MCP_TOOLS.md](reference/MCP_TOOLS.md) | MCP tool provider reference: ListAvailableApis, CallApi, SearchEndpoints |
| [CONFIGURATION.md](reference/CONFIGURATION.md) | All configuration settings for API, MCP server, and frontend |
| [TIER_LIMITS.md](reference/TIER_LIMITS.md) | Subscription tiers (Free, Pro, Enterprise) and their limits |

## Patterns

| Document | Description |
|----------|-------------|
| [SPLIT_STORAGE.md](patterns/SPLIT_STORAGE.md) | Split storage pattern for large APIs exceeding Cosmos DB 2MB limit |
| [POLYMORPHIC_AUTH.md](patterns/POLYMORPHIC_AUTH.md) | Polymorphic auth configuration with type discriminator serialization |
| [SPEC_PARSING_PIPELINE.md](patterns/SPEC_PARSING_PIPELINE.md) | OpenAPI, GraphQL, and Postman spec parsing pipeline |

## Guides

| Document | Description |
|----------|-------------|
| [LOCAL_DEVELOPMENT.md](guides/LOCAL_DEVELOPMENT.md) | Setting up local development environment |
| [DEPLOYMENT.md](guides/DEPLOYMENT.md) | Docker builds, Azure Container Registry, Container Apps deployment |
| [CI_CD_PIPELINE.md](guides/CI_CD_PIPELINE.md) | GitHub Actions CI/CD: build, test, Docker push, deploy |

## Solutions

| Document | Description |
|----------|-------------|
| [cosmos-2mb-document-limit.md](solutions/cosmos-2mb-document-limit.md) | How split storage solves Cosmos DB's 2MB document size limit |

## Navigation

- **New to the codebase?** Start with [SYSTEM_ARCHITECTURE.md](architecture/SYSTEM_ARCHITECTURE.md)
- **Task-based routing:** See [AI_NAVIGATION.md](AI_NAVIGATION.md)
