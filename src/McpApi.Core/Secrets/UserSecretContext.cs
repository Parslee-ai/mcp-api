namespace McpApi.Core.Secrets;

/// <summary>
/// Context required for resolving user-specific encrypted secrets.
/// </summary>
/// <param name="UserId">The user's unique identifier.</param>
/// <param name="EncryptionSalt">The user's encryption salt (base64).</param>
public record UserSecretContext(string UserId, string EncryptionSalt);
