namespace McpApi.Core.Secrets;

/// <summary>
/// Service for encrypting and decrypting sensitive data using per-user keys.
/// Uses AES-256-GCM with keys derived from a master key via HKDF.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext value for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="userSalt">The user's encryption salt (base64).</param>
    /// <param name="plaintext">The value to encrypt.</param>
    /// <returns>Encrypted data containing ciphertext, IV, and auth tag.</returns>
    EncryptedData Encrypt(string userId, string userSalt, string plaintext);

    /// <summary>
    /// Decrypts an encrypted value for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="userSalt">The user's encryption salt (base64).</param>
    /// <param name="encryptedData">The encrypted data to decrypt.</param>
    /// <returns>The decrypted plaintext value.</returns>
    string Decrypt(string userId, string userSalt, EncryptedData encryptedData);

    /// <summary>
    /// Generates a new random salt for a user.
    /// </summary>
    /// <returns>Base64-encoded random salt.</returns>
    string GenerateSalt();
}

/// <summary>
/// Represents encrypted data with all components needed for decryption.
/// </summary>
public class EncryptedData
{
    /// <summary>Base64-encoded encrypted value.</summary>
    public required string Ciphertext { get; set; }

    /// <summary>Base64-encoded initialization vector.</summary>
    public required string Iv { get; set; }

    /// <summary>Base64-encoded authentication tag.</summary>
    public required string AuthTag { get; set; }
}
