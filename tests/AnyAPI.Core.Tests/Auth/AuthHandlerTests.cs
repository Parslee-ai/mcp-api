namespace AnyAPI.Core.Tests.Auth;

using AnyAPI.Core.Auth;
using AnyAPI.Core.Models;
using AnyAPI.Core.Secrets;
using System.Text;
using Xunit;

/// <summary>
/// Tests for authentication handlers.
/// </summary>
public class AuthHandlerTests
{
    private readonly MockSecretProvider _secretProvider = new();

    #region ApiKeyAuthHandler Tests

    [Fact]
    public async Task ApiKeyAuthHandler_WithHeaderPlacement_AddsHeader()
    {
        // Arrange
        _secretProvider.SetSecret("api-key-secret", "test-api-key-123");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "X-API-Key",
            In = "header",
            Secret = new SecretReference { SecretName = "api-key-secret" }
        };
        var handler = new ApiKeyAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("api-key-secret", "test-api-key-123");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "api_key",
            In = "query",
            Secret = new SecretReference { SecretName = "api-key-secret" }
        };
        var handler = new ApiKeyAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("api-key-secret", "my-key");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "key",
            In = "query",
            Secret = new SecretReference { SecretName = "api-key-secret" }
        };
        var handler = new ApiKeyAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("session-secret", "session-token-abc");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "session_id",
            In = "cookie",
            Secret = new SecretReference { SecretName = "session-secret" }
        };
        var handler = new ApiKeyAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("api-key-secret", "test-key");
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "key",
            In = "unknown",
            Secret = new SecretReference { SecretName = "api-key-secret" }
        };
        var handler = new ApiKeyAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("bearer-token", "jwt-token-xyz");
        var config = new BearerTokenAuthConfig
        {
            Prefix = "Bearer",
            Secret = new SecretReference { SecretName = "bearer-token" }
        };
        var handler = new BearerTokenAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("token", "my-token");
        var config = new BearerTokenAuthConfig
        {
            Prefix = "Token",
            Secret = new SecretReference { SecretName = "token" }
        };
        var handler = new BearerTokenAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("username", "testuser");
        _secretProvider.SetSecret("password", "testpass");
        var config = new BasicAuthConfig
        {
            Username = new SecretReference { SecretName = "username" },
            Password = new SecretReference { SecretName = "password" }
        };
        var handler = new BasicAuthHandler(config, _secretProvider);
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
        _secretProvider.SetSecret("username", "user@example.com");
        _secretProvider.SetSecret("password", "p@ss:word!");
        var config = new BasicAuthConfig
        {
            Username = new SecretReference { SecretName = "username" },
            Password = new SecretReference { SecretName = "password" }
        };
        var handler = new BasicAuthHandler(config, _secretProvider);
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
        var factory = new AuthHandlerFactory(_secretProvider, new MockHttpClientFactory());
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
        var factory = new AuthHandlerFactory(_secretProvider, new MockHttpClientFactory());
        var config = new ApiKeyAuthConfig
        {
            ParameterName = "X-API-Key",
            In = "header",
            Secret = new SecretReference { SecretName = "key" }
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
        var factory = new AuthHandlerFactory(_secretProvider, new MockHttpClientFactory());
        var config = new BearerTokenAuthConfig
        {
            Prefix = "Bearer",
            Secret = new SecretReference { SecretName = "token" }
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
        var factory = new AuthHandlerFactory(_secretProvider, new MockHttpClientFactory());
        var config = new BasicAuthConfig
        {
            Username = new SecretReference { SecretName = "user" },
            Password = new SecretReference { SecretName = "pass" }
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
        var factory = new AuthHandlerFactory(_secretProvider, new MockHttpClientFactory());
        var config = new OAuth2AuthConfig
        {
            Flow = "clientCredentials",
            TokenUrl = "https://auth.example.com/token",
            ClientId = new SecretReference { SecretName = "client-id" },
            ClientSecret = new SecretReference { SecretName = "client-secret" }
        };

        // Act
        var handler1 = factory.Create(config);
        var handler2 = factory.Create(config);

        // Assert - same instance should be returned (cached)
        Assert.Same(handler1, handler2);
    }

    #endregion

    #region Mock Implementations

    private class MockSecretProvider : ISecretProvider
    {
        private readonly Dictionary<string, string> _secrets = new();

        public void SetSecret(string name, string value) => _secrets[name] = value;

        public Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
        {
            if (_secrets.TryGetValue(secretName, out var value))
                return Task.FromResult(value);
            throw new KeyNotFoundException($"Secret '{secretName}' not found");
        }

        public Task<string?> TryGetSecretAsync(string secretName, CancellationToken ct = default)
        {
            _secrets.TryGetValue(secretName, out var value);
            return Task.FromResult(value);
        }

        public Task SetSecretAsync(string secretName, string value, CancellationToken ct = default)
        {
            _secrets[secretName] = value;
            return Task.CompletedTask;
        }

        public Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
        {
            _secrets.Remove(secretName);
            return Task.CompletedTask;
        }
    }

    private class MockHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    #endregion
}
