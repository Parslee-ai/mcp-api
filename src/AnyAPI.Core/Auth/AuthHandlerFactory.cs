namespace AnyAPI.Core.Auth;

using AnyAPI.Core.Models;
using AnyAPI.Core.Secrets;

/// <summary>
/// Factory for creating auth handlers based on configuration type.
/// </summary>
public class AuthHandlerFactory : IAuthHandlerFactory
{
    private readonly ISecretProvider _secretProvider;
    private readonly HttpClient _httpClient;

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
            OAuth2AuthConfig oauth2 => new OAuth2AuthHandler(oauth2, _secretProvider, _httpClient),
            _ => throw new NotSupportedException($"Auth type {config.GetType().Name} is not supported")
        };
    }
}

/// <summary>
/// Interface for the auth handler factory.
/// </summary>
public interface IAuthHandlerFactory
{
    IAuthHandler Create(AuthConfiguration config);
}
