namespace McpApi.Core.Tests.IntegrationTests;

using System.Text.Json;
using McpApi.Core.Models;
using McpApi.Core.OpenApi;
using Xunit;

public class GitHubApiRegistrationTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task CanParseGitHubApiSpec()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);

        // Act
        var registration = await parser.ParseAsync(
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json");

        // Assert
        Assert.NotNull(registration);
        Assert.StartsWith("github", registration.Id);
        Assert.Contains("GitHub", registration.DisplayName);
        Assert.True(registration.Endpoints.Count > 900, $"Expected >900 endpoints, got {registration.Endpoints.Count}");
    }

    [Fact]
    public async Task AuthConfigurationSerializesCorrectly()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);
        var registration = await parser.ParseAsync(
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json");

        // Act - serialize
        var authJson = JsonSerializer.Serialize(registration.Auth, _jsonOptions);

        // Assert - has discriminator
        Assert.Contains("authType", authJson);

        // Act - deserialize
        var deserialized = JsonSerializer.Deserialize<AuthConfiguration>(authJson, _jsonOptions);

        // Assert - round-trips correctly
        Assert.NotNull(deserialized);
        Assert.Equal(registration.Auth.AuthType, deserialized.AuthType);
    }

    [Fact]
    public void AuthConfigurationHandlesMissingDiscriminator()
    {
        // Arrange - JSON without authType discriminator (like old Cosmos data)
        var jsonWithoutDiscriminator = """{"name": "test-auth"}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<AuthConfiguration>(jsonWithoutDiscriminator, _jsonOptions);

        // Assert - defaults to NoAuthConfig
        Assert.NotNull(deserialized);
        Assert.IsType<NoAuthConfig>(deserialized);
        Assert.Equal("none", deserialized.AuthType);
    }

    [Fact]
    public void AllAuthTypesSerializeCorrectly()
    {
        // Test all auth types
        var authConfigs = new AuthConfiguration[]
        {
            new NoAuthConfig { Name = "no-auth" },
            new ApiKeyAuthConfig
            {
                Name = "api-key",
                In = "header",
                ParameterName = "X-API-Key",
                Secret = new SecretReference { SecretName = "test-secret" }
            },
            new BearerTokenAuthConfig
            {
                Name = "bearer",
                Secret = new SecretReference { SecretName = "test-token" }
            },
            new BasicAuthConfig
            {
                Name = "basic",
                Username = new SecretReference { SecretName = "user" },
                Password = new SecretReference { SecretName = "pass" }
            },
            new OAuth2AuthConfig
            {
                Name = "oauth2",
                Flow = "clientCredentials",
                TokenUrl = "https://example.com/token",
                ClientId = new SecretReference { SecretName = "client-id" },
                ClientSecret = new SecretReference { SecretName = "client-secret" }
            }
        };

        foreach (var auth in authConfigs)
        {
            // Serialize
            var json = JsonSerializer.Serialize(auth, _jsonOptions);
            Assert.Contains($"\"authType\":\"{auth.AuthType}\"", json);

            // Deserialize
            var deserialized = JsonSerializer.Deserialize<AuthConfiguration>(json, _jsonOptions);
            Assert.NotNull(deserialized);
            Assert.Equal(auth.GetType(), deserialized.GetType());
            Assert.Equal(auth.AuthType, deserialized.AuthType);
        }
    }

    [Fact]
    public async Task FullRegistrationSerializesCorrectly()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);
        var registration = await parser.ParseAsync(
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json");

        // Save endpoints count before clearing for metadata-only serialization
        var endpointCount = registration.Endpoints.Count;
        registration.Endpoints = []; // Clear for smaller JSON

        // Act - serialize full registration
        var json = JsonSerializer.Serialize(registration, _jsonOptions);

        // Assert - has auth with discriminator
        Assert.Contains("authType", json);

        // Act - deserialize
        var deserialized = JsonSerializer.Deserialize<ApiRegistration>(json, _jsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(registration.Id, deserialized.Id);
        Assert.NotNull(deserialized.Auth);
        Assert.Equal(registration.Auth.AuthType, deserialized.Auth.AuthType);
    }

    [Fact]
    public async Task EndpointsSerializeWithCosmosOptions()
    {
        // Arrange - use the same serializer options as CosmosSystemTextJsonSerializer
        var cosmosOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);
        var registration = await parser.ParseAsync(
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json");

        // Get first 10 endpoints to test
        var endpoints = registration.Endpoints.Take(10).ToList();
        Assert.True(endpoints.Count > 0, "Should have at least some endpoints");

        // Test each endpoint serializes correctly
        foreach (var endpoint in endpoints)
        {
            // Set the apiId as the store would
            endpoint.ApiId = registration.Id;

            // Serialize
            var json = JsonSerializer.Serialize(endpoint, cosmosOptions);

            // Assert - has required Cosmos fields
            Assert.Contains("\"id\":", json);
            Assert.Contains("\"apiId\":", json);

            // Deserialize
            var deserialized = JsonSerializer.Deserialize<ApiEndpoint>(json, cosmosOptions);
            Assert.NotNull(deserialized);
            Assert.Equal(endpoint.Id, deserialized.Id);
            Assert.Equal(endpoint.ApiId, deserialized.ApiId);
            Assert.Equal(endpoint.OperationId, deserialized.OperationId);
            Assert.Equal(endpoint.Method, deserialized.Method);
            Assert.Equal(endpoint.Path, deserialized.Path);
        }
    }

    [Fact]
    public async Task ComplexEndpointWithParametersSerializes()
    {
        // Arrange
        var cosmosOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);
        var registration = await parser.ParseAsync(
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json");

        // Find an endpoint with parameters and request body
        var complexEndpoint = registration.Endpoints
            .FirstOrDefault(e => e.Parameters.Count > 0 && e.RequestBody != null);

        if (complexEndpoint == null)
        {
            // Find one with just parameters
            complexEndpoint = registration.Endpoints
                .FirstOrDefault(e => e.Parameters.Count > 2);
        }

        Assert.NotNull(complexEndpoint);
        complexEndpoint.ApiId = registration.Id;

        // Serialize
        var json = JsonSerializer.Serialize(complexEndpoint, cosmosOptions);

        // Assert - can serialize complex structures
        Assert.Contains("parameters", json);

        // Deserialize
        var deserialized = JsonSerializer.Deserialize<ApiEndpoint>(json, cosmosOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(complexEndpoint.Parameters.Count, deserialized.Parameters.Count);
    }

    [Fact]
    public async Task AllEndpointsSerializeWithinCosmosLimits()
    {
        // Arrange - Cosmos has a 2MB document size limit
        const int maxDocumentSizeBytes = 2 * 1024 * 1024;

        var cosmosOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        using var httpClient = new HttpClient();
        var parser = new OpenApiParser(httpClient);
        var registration = await parser.ParseAsync(
            "https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json");

        var oversizedEndpoints = new List<string>();
        var maxSize = 0;

        foreach (var endpoint in registration.Endpoints)
        {
            endpoint.ApiId = registration.Id;
            var json = JsonSerializer.Serialize(endpoint, cosmosOptions);
            var size = System.Text.Encoding.UTF8.GetByteCount(json);

            if (size > maxSize) maxSize = size;

            if (size > maxDocumentSizeBytes)
            {
                oversizedEndpoints.Add($"{endpoint.Id}: {size / 1024}KB");
            }
        }

        // Assert
        Assert.True(oversizedEndpoints.Count == 0,
            $"Found {oversizedEndpoints.Count} endpoints exceeding 2MB limit: {string.Join(", ", oversizedEndpoints.Take(5))}. Max size: {maxSize / 1024}KB");
    }
}
