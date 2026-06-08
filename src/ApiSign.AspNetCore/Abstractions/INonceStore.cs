namespace ApiSign.AspNetCore.Abstractions;

/// <summary>
/// Stores request nonce values for replay protection.
/// </summary>
public interface INonceStore
{
    /// <summary>
    /// Checks whether the nonce already exists.
    /// </summary>
    Task<bool> ExistsAsync(string nonce);

    /// <summary>
    /// Saves the nonce with an expiration period.
    /// </summary>
    Task SaveAsync(string nonce, TimeSpan expireTime);
}