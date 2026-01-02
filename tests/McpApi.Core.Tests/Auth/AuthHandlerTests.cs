namespace McpApi.Core.Tests.Auth;

using McpApi.Core.Auth;
using McpApi.Core.Models;
using McpApi.Core.Secrets;
using System.Text;
using Xunit;

/// <summary>
/// Tests for authentication handlers.
/// </summary>
public class AuthHandlerTests
{
    private readonly MockSecretResolver _secretResolver = new();

    #region ApiKeyAuthHandler Tests

    [Fact]
    public async Task ApiKeyAuthHandler_WithHeaderPlacement_AddsHeader()
    {
        // Arrange
        _secretResolver.SetSecret("api-key-secret", "test-api-key-123");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "X-API-Key",
            In = "header",
            Secret = SecretReference.FromKeyVault("api-key-secret")
        };
        var handler = new ApiKeyAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.True(request.Headers.TryGetValues("X-API-Key", out var values));
        Assert.Equal("test-api-key-123", values.Single());
    }

    [Fact]
    public async Task ApiKeyAuthHandler_WithQueryPlacement_AddsQueryParameter()
    {
        // Arrange
        _secretResolver.SetSecret("api-key-secret", "test-api-key-123");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "api_key",
            In = "query",
            Secret = SecretReference.FromKeyVault("api-key-secret")
        };
        var handler = new ApiKeyAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.Contains("api_key=test-api-key-123", request.RequestUri!.Query);
    }

    [Fact]
    public async Task ApiKeyAuthHandler_WithQueryPlacement_PreservesExistingQuery()
    {
        // Arrange
        _secretResolver.SetSecret("api-key-secret", "my-key");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "key",
            In = "query",
            Secret = SecretReference.FromKeyVault("api-key-secret")
        };
        var handler = new ApiKeyAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users?page=1");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.Contains("page=1", request.RequestUri!.Query);
        Assert.Contains("key=my-key", request.RequestUri!.Query);
    }

    [Fact]
    public async Task ApiKeyAuthHandler_WithCookiePlacement_AddsCookieHeader()
    {
        // Arrange
        _secretResolver.SetSecret("session-secret", "session-token-abc");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "session_id",
            In = "cookie",
            Secret = SecretReference.FromKeyVault("session-secret")
        };
        var handler = new ApiKeyAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.True(request.Headers.TryGetValues("Cookie", out var values));
        Assert.Equal("session_id=session-token-abc", values.Single());
    }

    [Fact]
    public async Task ApiKeyAuthHandler_WithUnknownPlacement_ThrowsInvalidOperationException()
    {
        // Arrange
        _secretResolver.SetSecret("api-key-secret", "test-key");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "key",
            In = "unknown",
            Secret = SecretReference.FromKeyVault("api-key-secret")
        };
        var handler = new ApiKeyAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ApplyAuthAsync(request));
    }

    #endregion

    #region BearerTokenAuthHandler Tests

    [Fact]
    public async Task BearerTokenAuthHandler_AddsAuthorizationHeader()
    {
        // Arrange
        _secretResolver.SetSecret("bearer-token", "jwt-token-xyz");
        var config = new BearerTokenAuthConfig
        {
            Prefix = "Bearer",
            Secret = SecretReference.FromKeyVault("bearer-token")
        };
        var handler = new BearerTokenAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("jwt-token-xyz", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task BearerTokenAuthHandler_WithCustomPrefix_UsesCustomPrefix()
    {
        // Arrange
        _secretResolver.SetSecret("token", "my-token");
        var config = new BearerTokenAuthConfig
        {
            Prefix = "Token",
            Secret = SecretReference.FromKeyVault("token")
        };
        var handler = new BearerTokenAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.Equal("Token", request.Headers.Authorization!.Scheme);
        Assert.Equal("my-token", request.Headers.Authorization.Parameter);
    }

    #endregion

    #region BasicAuthHandler Tests

    [Fact]
    public async Task BasicAuthHandler_AddsBasicAuthHeader()
    {
        // Arrange
        _secretResolver.SetSecret("username", "testuser");
        _secretResolver.SetSecret("password", "testpass");
        var config = new BasicAuthConfig
        {
            Username = SecretReference.FromKeyVault("username"),
            Password = SecretReference.FromKeyVault("password")
        };
        var handler = new BasicAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Basic", request.Headers.Authorization.Scheme);

        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:testpass"));
        Assert.Equal(expectedCredentials, request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task BasicAuthHandler_EncodesSpecialCharacters()
    {
        // Arrange
        _secretResolver.SetSecret("username", "user@example.com");
        _secretResolver.SetSecret("password", "p@ss:word!");
        var config = new BasicAuthConfig
        {
            Username = SecretReference.FromKeyVault("username"),
            Password = SecretReference.FromKeyVault("password")
        };
        var handler = new BasicAuthHandler(config, _secretResolver, null);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("user@example.com:p@ss:word!"));
        Assert.Equal(expectedCredentials, request.Headers.Authorization!.Parameter);
    }

    #endregion

    #region NoOpAuthHandler Tests

    [Fact]
    public async Task NoOpAuthHandler_DoesNotModifyRequest()
    {
        // Arrange
        var handler = new NoOpAuthHandler();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
        var originalHeaderCount = request.Headers.Count();

        // Act
        await handler.ApplyAuthAsync(request);

        // Assert
        Assert.Equal(originalHeaderCount, request.Headers.Count());
        Assert.Null(request.Headers.Authorization);
    }

    #endregion

    #region AuthHandlerFactory Tests

    [Fact]
    public void AuthHandlerFactory_WithNoAuthConfig_ReturnsNoOpHandler()
    {
        // Arrange
        var factory = new AuthHandlerFactory(_secretResolver, new MockHttpClientFactory());
        var config = new NoAuthConfig();

        // Act
        var handler = factory.Create(config);

        // Assert
        Assert.IsType<NoOpAuthHandler>(handler);
    }

    [Fact]
    public void AuthHandlerFactory_WithApiKeyConfig_ReturnsApiKeyHandler()
    {
        // Arrange
        var factory = new AuthHandlerFactory(_secretResolver, new MockHttpClientFactory());
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "X-API-Key",
            In = "header",
            Secret = SecretReference.FromKeyVault("key")
        };

        // Act
        var handler = factory.Create(config);

        // Assert
        Assert.IsType<ApiKeyAuthHandler>(handler);
    }

    [Fact]
    public void AuthHandlerFactory_WithBearerConfig_ReturnsBearerHandler()
    {
        // Arrange
        var factory = new AuthHandlerFactory(_secretResolver, new MockHttpClientFactory());
        var config = new BearerTokenAuthConfig
        {
            Prefix = "Bearer",
            Secret = SecretReference.FromKeyVault("token")
        };

        // Act
        var handler = factory.Create(config);

        // Assert
        Assert.IsType<BearerTokenAuthHandler>(handler);
    }

    [Fact]
    public void AuthHandlerFactory_WithBasicConfig_ReturnsBasicHandler()
    {
        // Arrange
        var factory = new AuthHandlerFactory(_secretResolver, new MockHttpClientFactory());
        var config = new BasicAuthConfig
        {
            Username = SecretReference.FromKeyVault("user"),
            Password = SecretReference.FromKeyVault("pass")
        };

        // Act
        var handler = factory.Create(config);

        // Assert
        Assert.IsType<BasicAuthHandler>(handler);
    }

    [Fact]
    public void AuthHandlerFactory_CachesOAuth2Handlers()
    {
        // Arrange
        var factory = new AuthHandlerFactory(_secretResolver, new MockHttpClientFactory());
        var config = new OAuth2AuthConfig
        {
            Flow = "clientCredentials",
            TokenUrl = "https://auth.example.com/token",
            ClientId = SecretReference.FromKeyVault("client-id"),
            ClientSecret = SecretReference.FromKeyVault("client-secret")
        };

        // Act
        var handler1 = factory.Create(config);
        var handler2 = factory.Create(config);

        // Assert - same instance should be returned (cached)
        Assert.Same(handler1, handler2);
    }

    #endregion

    #region Mock Implementations

    private class MockSecretResolver : ISecretResolver
    {
        private readonly Dictionary<string, string> _secrets = new();

        public void SetSecret(string name, string value) => _secrets[name] = value;

        public Task<string> ResolveAsync(SecretReference reference, string userId, string userSalt, CancellationToken ct = default)
        {
            // For Key Vault references, use SecretName
            if (reference.IsKeyVaultReference && _secrets.TryGetValue(reference.SecretName!, out var value))
                return Task.FromResult(value);

            throw new KeyNotFoundException($"Secret not found");
        }

        public SecretReference Encrypt(string plaintext, string userId, string userSalt)
        {
            // Not needed for these tests
            throw new NotImplementedException();
        }
    }

    private class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    #endregion
}
