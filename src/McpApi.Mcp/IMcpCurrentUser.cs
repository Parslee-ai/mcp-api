namespace McpApi.Mcp;

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
}

/// <summary>
/// Simple implementation that reads user ID from environment variable.
/// Will be replaced with token-based auth in Phase 6.
/// </summary>
public class EnvironmentMcpCurrentUser : IMcpCurrentUser
{
    private readonly string _userId;

    public EnvironmentMcpCurrentUser()
    {
        _userId = Environment.GetEnvironmentVariable("MCPAPI_USER_ID")
            ?? throw new InvalidOperationException(
                "MCPAPI_USER_ID environment variable is required. " +
                "In Phase 6, this will be replaced with token-based authentication.");
    }

    public string UserId => _userId;
}
