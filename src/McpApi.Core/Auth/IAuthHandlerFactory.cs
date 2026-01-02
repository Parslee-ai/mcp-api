namespace McpApi.Core.Auth;

using McpApi.Core.Models;

/// <summary>
/// Factory interface for creating auth handlers based on configuration type.
/// </summary>
public interface IAuthHandlerFactory
{
    /// <summary>
    /// Creates an auth handler for the specified configuration.
    /// </summary>
    /// <param name="config">The authentication configuration.</param>
    /// <returns>An auth handler that can apply authentication to requests.</returns>
    IAuthHandler Create(AuthConfiguration config);
}
