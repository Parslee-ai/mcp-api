namespace McpApi.Core.Auth;

/// <summary>
/// Utility for hashing and verifying passwords using BCrypt.
/// </summary>
public static class PasswordHasher
{
    private const int WorkFactor = 12;

    /// <summary>
    /// Hashes a password using BCrypt.
    /// </summary>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    /// <summary>
    /// Verifies a password against a BCrypt hash.
    /// </summary>
    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
