namespace McpApi.Mcp;

using McpApi.Core.Auth;
using McpApi.Core.Models;
using McpApi.Core.Secrets;
using McpApi.Core.Storage;

/// <summary>
/// Service for accessing the current MCP user context.
/// Supports token-based authentication via MCPAPI_TOKEN environment variable.
/// </summary>
public interface IMcpCurrentUser
{
    /// <summary>
    /// Gets the current user's ID.
    /// Throws if not authenticated.
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// Gets the user's subscription tier (free, pro, enterprise).
    /// </summary>
    string Tier { get; }

    /// <summary>
    /// Gets the user's secret context for decrypting API secrets.
    /// May be null if encryption is not configured.
    /// </summary>
    UserSecretContext? SecretContext { get; }
}

/// <summary>
/// Token-based authentication implementation.
/// Validates MCPAPI_TOKEN environment variable and loads user context.
/// </summary>
public class TokenMcpCurrentUser : IMcpCurrentUser
{
    private readonly string _userId;
    private readonly string _tier;
    private readonly UserSecretContext? _secretContext;

    public TokenMcpCurrentUser(
        IMcpTokenService tokenService,
        IUserStore userStore)
    {
        var token = Environment.GetEnvironmentVariable("MCPAPI_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "MCPAPI_TOKEN environment variable is required for MCP authentication. " +
                "Create a token in the MCP-API web interface and set it as MCPAPI_TOKEN.");
        }

        // Validate token synchronously (blocking call on startup is acceptable)
        var validatedToken = tokenService.ValidateTokenAsync(token).GetAwaiter().GetResult();
        if (validatedToken == null)
        {
            throw new InvalidOperationException(
                "Invalid or expired MCPAPI_TOKEN. Please create a new token in the MCP-API web interface.");
        }

        _userId = validatedToken.UserId;

        // Load user details to get tier and encryption salt
        var user = userStore.GetByIdAsync(_userId).GetAwaiter().GetResult();
        if (user == null)
        {
            throw new InvalidOperationException(
                $"User account not found for token. The account may have been deleted.");
        }

        _tier = user.Tier;

        // Set up encryption context if user has encryption salt
        if (!string.IsNullOrEmpty(user.EncryptionKeySalt))
        {
            _secretContext = new UserSecretContext(_userId, user.EncryptionKeySalt);
        }
    }

    public string UserId => _userId;
    public string Tier => _tier;
    public UserSecretContext? SecretContext => _secretContext;
}

/// <summary>
/// Legacy implementation that reads user context from environment variables.
/// Kept for backwards compatibility and development scenarios.
/// </summary>
[Obsolete("Use TokenMcpCurrentUser for production. This is for development/testing only.")]
public class EnvironmentMcpCurrentUser : IMcpCurrentUser
{
    private readonly string _userId;
    private readonly string _tier;
    private readonly UserSecretContext? _secretContext;

    public EnvironmentMcpCurrentUser()
    {
        _userId = Environment.GetEnvironmentVariable("MCPAPI_USER_ID")
            ?? throw new InvalidOperationException(
                "MCPAPI_USER_ID environment variable is required. " +
                "For production, use MCPAPI_TOKEN instead.");

        _tier = Environment.GetEnvironmentVariable("MCPAPI_USER_TIER") ?? "free";

        var encryptionSalt = Environment.GetEnvironmentVariable("MCPAPI_ENCRYPTION_SALT");
        if (!string.IsNullOrEmpty(encryptionSalt))
        {
            _secretContext = new UserSecretContext(_userId, encryptionSalt);
        }
    }

    public string UserId => _userId;
    public string Tier => _tier;
    public UserSecretContext? SecretContext => _secretContext;
}
