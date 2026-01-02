namespace McpApi.Core.Auth;

using System.Collections.Concurrent;
using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Factory for creating auth handlers based on configuration type.
/// OAuth2 handlers are cached to preserve token state across requests.
/// </summary>
public class AuthHandlerFactory : IAuthHandlerFactory
{
    private readonly ISecretResolver _secretResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, OAuth2AuthHandler> _oauth2Cache = new();

    public AuthHandlerFactory(ISecretResolver secretResolver, IHttpClientFactory httpClientFactory)
    {
        _secretResolver = secretResolver;
        _httpClientFactory = httpClientFactory;
    }

    public IAuthHandler Create(AuthConfiguration config, UserSecretContext? userContext = null)
    {
        return config switch
        {
            NoAuthConfig => new NoOpAuthHandler(),
            ApiKeyAuthConfig apiKey => new ApiKeyAuthHandler(apiKey, _secretResolver, userContext),
            BearerTokenAuthConfig bearer => new BearerTokenAuthHandler(bearer, _secretResolver, userContext),
            BasicAuthConfig basic => new BasicAuthHandler(basic, _secretResolver, userContext),
            OAuth2AuthConfig oauth2 => GetOrCreateOAuth2Handler(oauth2, userContext),
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

    private OAuth2AuthHandler GetOrCreateOAuth2Handler(OAuth2AuthConfig config, UserSecretContext? userContext)
    {
        // Cache key based on token URL, client ID, and user context
        var secretId = config.ClientId.IsEncrypted
            ? config.ClientId.EncryptedValue![..8]  // Use partial encrypted value as key
            : config.ClientId.SecretName ?? "unknown";
        var userKey = userContext?.UserId ?? "global";
        var cacheKey = $"{config.TokenUrl}:{secretId}:{userKey}";

        return _oauth2Cache.GetOrAdd(cacheKey, _ =>
            new OAuth2AuthHandler(config, _secretResolver, userContext, _httpClientFactory.CreateClient(Constants.HttpClients.OAuth2)));
    }
}
