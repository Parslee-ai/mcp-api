namespace AnyAPI.Core.Secrets;

/// <summary>
/// Interface for retrieving secrets from a secure store.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Gets a secret value by name. Throws if not found.
    /// </summary>
    Task<string> GetSecretAsync(string secretName, CancellationToken ct = default);

    /// <summary>
    /// Tries to get a secret value by name. Returns null if not found.
    /// </summary>
    Task<string?> TryGetSecretAsync(string secretName, CancellationToken ct = default);

    /// <summary>
    /// Sets a secret value.
    /// </summary>
    Task SetSecretAsync(string secretName, string value, CancellationToken ct = default);

    /// <summary>
    /// Deletes a secret.
    /// </summary>
    Task DeleteSecretAsync(string secretName, CancellationToken ct = default);
}
