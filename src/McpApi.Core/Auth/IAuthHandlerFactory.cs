namespace McpApi.Core.Auth;

using McpApi.Core.Models;
using McpApi.Core.Secrets;

/// <summary>
/// Factory interface for creating auth handlers based on configuration type.
/// </summary>
public interface IAuthHandlerFactory
{
    /// <summary>
    /// Creates an auth handler for the specified configuration.
    /// </summary>
    /// <param name="config">The authentication configuration.</param>
    /// <param name="userContext">User context for decrypting user-specific secrets. Required if secrets are encrypted.</param>
    /// <returns>An auth handler that can apply authentication to requests.</returns>
    IAuthHandler Create(AuthConfiguration config, UserSecretContext? userContext = null);
}
