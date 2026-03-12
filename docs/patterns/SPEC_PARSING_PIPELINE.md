# Spec Parsing Pipeline

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Overview

MCP-API supports three API specification formats. Each parser converts a spec into the same `ApiRegistration` + `ApiEndpoint[]` model, enabling uniform tool generation.

## Supported Formats

| Format | Parser | Detection |
|--------|--------|-----------|
| OpenAPI 3.x / Swagger 2.0 | `OpenApiParser` | JSON/YAML with `openapi` or `swagger` field |
| GraphQL | `GraphQLSchemaParser` | URL ending in `/graphql`, or SDL with `type Query` |
| Postman Collection v2.1 | `PostmanCollectionParser` | JSON with `info.schema` containing `collection/v2` |

## OpenAPI Parsing Pipeline

**Files:** `src/McpApi.Core/OpenApi/`

```
OpenApiParser.ParseAsync(specUrl)
  → Fetch spec via HTTP
  → OpenApiStreamReader (Microsoft.OpenApi library)
  → Extract base URL from servers[]
  → Generate API ID from title + base URL
  → Extract auth from securitySchemes → AuthConfiguration
  → For each path + operation:
      OperationConverter.Convert(path, method, operation, pathParams)
        → Generate operationId if missing (e.g., "get_repos_owner_repo")
        → Merge path-level and operation-level parameters
        → SchemaFlattener.Flatten(schema)
            → Resolve $ref with circular reference protection
            → Depth limit: Constants.Schema.MaxFlattenDepth (10)
            → Convert to simplified JsonSchema model
        → Build ApiEndpoint with parameters, requestBody, responses
  → Return ApiRegistration with endpoints
```

### Schema Flattener

`SchemaFlattener` handles complex OpenAPI schemas:
- Resolves `$ref` references with visited-set tracking (prevents infinite loops)
- Enforces max depth of 10 levels
- Converts enum, array, object, and primitive types
- Preserves validation constraints (required, min/max, pattern)
- Thread-safe: each call creates independent visited-ref tracking

### OpenAPI Discovery

`OpenApiDiscovery` auto-discovers spec URLs when not provided:
- Probes common paths: `/openapi.json`, `/swagger.json`, `/api-docs`, `/v3/api-docs`
- Checks well-known APIs by domain (GitHub, Stripe, OpenAI, Slack, Twilio, Microsoft Graph, Spotify, Discord, Notion, Cloudflare)
- Validates discovered URLs with HEAD request and content-type check

### Auth Extraction

Maps OpenAPI `securitySchemes` to `AuthConfiguration`:

| OpenAPI Scheme | Auth Type |
|---------------|-----------|
| `apiKey` | `ApiKeyAuthConfig` (header/query/cookie) |
| `http` + `bearer` | `BearerTokenAuthConfig` |
| `http` + `basic` | `BasicAuthConfig` |
| `oauth2` | `OAuth2AuthConfig` (extracts token URL from flows) |

## GraphQL Parsing

**File:** `src/McpApi.Core/GraphQL/GraphQLSchemaParser.cs`

```
GraphQLSchemaParser.ParseFromEndpointAsync(graphqlUrl)
  → Send introspection query to endpoint
  → Parse Query and Mutation type fields
  → Each field becomes an ApiEndpoint:
      - Query fields → GET method
      - Mutation fields → POST method
      - Field arguments → ParameterDefinition
      - GraphQL types mapped to JSON types
  → Return ApiRegistration
```

Also supports SDL parsing via `ParseFromSdl()` using regex-based type/field extraction.

**Type mapping:** String→string, Int→integer, Float→number, Boolean→boolean, ID→string

## Postman Collection Parsing

**File:** `src/McpApi.Core/Postman/PostmanCollectionParser.cs`

```
PostmanCollectionParser.ParseAsync(url)
  → Fetch collection JSON
  → Flatten nested folders into flat endpoint list
  → Extract base URL from collection variables or first request
  → For each request item:
      - Convert path segments (replace {{var}} with collection variables)
      - Extract query/header parameters
      - Extract request body (JSON, form data, URL-encoded)
      - Infer JSON schemas from example values
  → Convert Postman auth to AuthConfiguration
  → Return ApiRegistration
```

**Variable substitution:** Replaces `{{variable}}` placeholders with values from `collection.variable[]`.

## Format Detection

The `ApisController.Register()` endpoint auto-detects the format:

1. Check if URL looks like a GraphQL endpoint → introspect
2. Fetch content from URL
3. Check if content is Postman Collection (`PostmanCollectionParser.IsPostmanCollection()`)
4. Default to OpenAPI parsing

## See Also

- [DATA_MODEL.md](../architecture/DATA_MODEL.md) - ApiEndpoint document schema
- [SPLIT_STORAGE.md](SPLIT_STORAGE.md) - How parsed endpoints are stored
- [POLYMORPHIC_AUTH.md](POLYMORPHIC_AUTH.md) - Auth configuration extracted from specs
