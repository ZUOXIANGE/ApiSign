using ApiSign.AspNetCore.Models;

namespace ApiSign.AspNetCore.Abstractions;

/// <summary>
/// Provides application signing secrets.
/// </summary>
public interface IAppSecretProvider
{
    /// <summary>
    /// Gets the application secret configuration for the specified application identifier.
    /// </summary>
    Task<AppSecretInfo?> GetAppSecretAsync(string appId);
}