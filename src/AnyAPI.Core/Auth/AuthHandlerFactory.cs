namespace AnyAPI.Core.Auth;

using System.Collections.Concurrent;
using AnyAPI.Core.Models;
using AnyAPI.Core.Secrets;

/// <summary>
/// Factory for creating auth handlers based on configuration type.
/// OAuth2 handlers are cached to preserve token state across requests.
/// </summary>
public class AuthHandlerFactory : IAuthHandlerFactory
{
    private readonly ISecretProvider _secretProvider;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, OAuth2AuthHandler> _oauth2Cache = new();

    public AuthHandlerFactory(ISecretProvider secretProvider, HttpClient httpClient)
    {
        _secretProvider = secretProvider;
        _httpClient = httpClient;
    }

    public IAuthHandler Create(AuthConfiguration config)
    {
        return config switch
        {
            NoAuthConfig => new NoOpAuthHandler(),
            ApiKeyAuthConfig apiKey => new ApiKeyAuthHandler(apiKey, _secretProvider),
            BearerTokenAuthConfig bearer => new BearerTokenAuthHandler(bearer, _secretProvider),
            BasicAuthConfig basic => new BasicAuthHandler(basic, _secretProvider),
            OAuth2AuthConfig oauth2 => GetOrCreateOAuth2Handler(oauth2),
            _ => throw new NotSupportedException($"Auth type {config.GetType().Name} is not supported")
        };
    }

    /// <summary>
    /// Invalidates cached OAuth2 handler when auth config changes.
    /// </summary>
    public void InvalidateOAuth2Cache(string cacheKey)
    {
        _oauth2Cache.TryRemove(cacheKey, out _);
    }

    private OAuth2AuthHandler GetOrCreateOAuth2Handler(OAuth2AuthConfig config)
    {
        // Cache key based on token URL and client ID secret name
        var cacheKey = $"{config.TokenUrl}:{config.ClientId.SecretName}";

        return _oauth2Cache.GetOrAdd(cacheKey, _ =>
            new OAuth2AuthHandler(config, _secretProvider, _httpClient));
    }
}

/// <summary>
/// Interface for the auth handler factory.
/// </summary>
public interface IAuthHandlerFactory
{
    IAuthHandler Create(AuthConfiguration config);
}
