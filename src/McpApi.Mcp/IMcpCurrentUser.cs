namespace McpApi.Mcp;

using McpApi.Core.Secrets;

/// <summary>
/// Service for accessing the current MCP user context.
/// Phase 6 will implement token-based authentication.
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
/// Simple implementation that reads user context from environment variables.
/// Will be replaced with token-based auth in Phase 6.
/// </summary>
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
                "In Phase 6, this will be replaced with token-based authentication.");

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
