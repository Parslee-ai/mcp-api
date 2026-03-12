# Polymorphic Auth Configuration

**Last Updated**: 2026-03-12
**Status**: Current
**Owner**: DocPipe

## Problem

API registrations support multiple authentication types (none, API key, bearer token, basic, OAuth2). These must serialize/deserialize correctly to Cosmos DB and handle legacy documents that may lack type discriminators.

## Solution

Abstract base class `AuthConfiguration` with concrete subtypes and a custom JSON converter using an `authType` discriminator field.

### Type Hierarchy

```
AuthConfiguration (abstract)
  ├── NoAuthConfig         (authType: "none")
  ├── ApiKeyAuthConfig     (authType: "apiKey")
  ├── BearerTokenAuthConfig (authType: "bearerToken")
  ├── BasicAuthConfig      (authType: "basic")
  └── OAuth2AuthConfig     (authType: "oauth2")
```

### Discriminator Mapping

| `authType` Value | Class | Key Properties |
|-----------------|-------|----------------|
| `none` | `NoAuthConfig` | (none) |
| `apiKey` | `ApiKeyAuthConfig` | `KeyName`, `KeyLocation` (header/query/cookie), `ApiKey: SecretReference` |
| `bearerToken` | `BearerTokenAuthConfig` | `Token: SecretReference` |
| `basic` | `BasicAuthConfig` | `Username`, `Password: SecretReference` |
| `oauth2` | `OAuth2AuthConfig` | `TokenUrl`, `ClientId`, `ClientSecret: SecretReference`, `Scopes`, `GrantType` |

### Secret Storage

Sensitive fields use `SecretReference` instead of plain strings. This supports:
- **Encrypted mode** (preferred): AES-256-GCM encrypted values stored in Cosmos DB
- **Key Vault mode** (legacy): References to Azure Key Vault secrets

## Implementation

**File:** `src/McpApi.Core/Models/AuthConfiguration.cs`

### Custom JSON Converter

`AuthConfigurationConverter` handles serialization with these rules:

1. **Read:** Reads `authType` from JSON, maps to concrete type, deserializes remaining properties
2. **Write:** Writes `authType` discriminator plus type-specific properties
3. **Fallback:** Returns `NoAuthConfig` if `authType` is missing or unrecognized (handles legacy data)

### Cosmos DB Integration

Cosmos DB must use `CosmosSystemTextJsonSerializer` (not Newtonsoft.Json) for proper polymorphic serialization. This is configured in `Program.cs` when creating the `CosmosClient`:

```csharp
new CosmosClientOptions
{
    Serializer = new CosmosSystemTextJsonSerializer(jsonOptions)
}
```

### Auth Handler Factory

`AuthHandlerFactory.Create()` maps configuration types to handler implementations:

| Config Type | Handler | Behavior |
|------------|---------|----------|
| `NoAuthConfig` | `NoOpAuthHandler` | No-op |
| `ApiKeyAuthConfig` | `ApiKeyAuthHandler` | Adds key to header/query/cookie |
| `BearerTokenAuthConfig` | `BearerTokenAuthHandler` | Adds `Authorization: Bearer` header |
| `BasicAuthConfig` | `BasicAuthHandler` | Adds `Authorization: Basic` header |
| `OAuth2AuthConfig` | `OAuth2AuthHandler` | Client credentials/auth code flow with token caching |

OAuth2 handlers are cached in a `ConcurrentDictionary` to preserve access token state across API calls.

## Adding a New Auth Type

1. Add new concrete class extending `AuthConfiguration` in `AuthConfiguration.cs`
2. Add `authType` discriminator value
3. Update `AuthConfigurationConverter` read/write methods
4. Create new `IAuthHandler` implementation in `McpApi.Core/Auth/`
5. Add case to `AuthHandlerFactory.Create()`
6. Update `OpenApiParser.ExtractAuthConfiguration()` if mapping from OpenAPI security schemes

## See Also

- [SECURITY_MODEL.md](../architecture/SECURITY_MODEL.md) - Encryption and secret management
- [DATA_MODEL.md](../architecture/DATA_MODEL.md) - How auth configs are stored in ApiRegistration documents
