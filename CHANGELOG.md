# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-01

### Added

- **Dynamic API Registration** - Register any REST API by providing an OpenAPI specification URL
- **OpenAPI 3.x Support** - Full parsing of OpenAPI 3.0 and 3.1 specifications
- **Swagger 2.0 Support** - Backwards compatibility with Swagger 2.0 specs
- **Postman Collection Support** - Import APIs from Postman Collection v2.1 format
- **GraphQL Support** - Register GraphQL APIs via introspection or SDL
- **Auto-Discovery** - Automatically discover OpenAPI specs from common locations
- **Well-Known APIs** - Pre-configured spec URLs for GitHub, Stripe, OpenAI, Slack, Twilio, Microsoft Graph, Spotify, Discord, Notion, and Cloudflare
- **Authentication Support**
  - API Key (header, query, cookie)
  - Bearer Token
  - Basic Auth
  - OAuth2 Client Credentials flow with automatic token refresh
- **Azure Integration**
  - Cosmos DB for API and endpoint storage
  - Azure Key Vault for secure secret management
- **Blazor Web UI** - Modern web interface for API management
- **MCP Server** - Expose registered APIs as tools for AI agents
- **SSRF Protection** - URL validation to prevent server-side request forgery
- **Endpoint Search** - Search across all enabled endpoints with optimized queries

### Security

- SSRF protection with comprehensive URL validation (blocks private IPs, localhost, cloud metadata endpoints)
- OAuth2 token caching with thread-safe refresh
- Secure secret storage via Azure Key Vault references
- URL-based hashing for unique API IDs (prevents collision attacks)

### Infrastructure

- GitHub Actions CI pipeline
- Dependabot for automated dependency updates
- EditorConfig for consistent code style
- MIT License

[1.0.0]: https://github.com/Parslee-ai/anyapi/releases/tag/v1.0.0
