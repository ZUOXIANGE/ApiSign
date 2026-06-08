namespace ApiSign.AspNetCore.Models;

/// <summary>
/// Represents application secret metadata.
/// </summary>
public sealed class AppSecretInfo
{
    public string AppId { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public SignAlgorithm Algorithm { get; set; } = SignAlgorithm.HMACSHA256;
}