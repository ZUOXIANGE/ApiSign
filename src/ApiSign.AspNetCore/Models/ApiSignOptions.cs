namespace ApiSign.AspNetCore.Models;

/// <summary>
/// Configures API signature validation behavior.
/// </summary>
public sealed class ApiSignOptions
{
    public bool Enabled { get; set; } = true;

    public int TimestampDisparitySeconds { get; set; } = 900;

    public bool EnableNonce { get; set; } = true;

    public int NonceExpireSeconds { get; set; } = 900;

    public SignAlgorithm DefaultAlgorithm { get; set; } = SignAlgorithm.HMACSHA256;

    public bool StrictMode { get; set; }

    public string AppIdParamName { get; set; } = "appId";

    public string NonceParamName { get; set; } = "nonce";

    public string TimestampParamName { get; set; } = "timestamp";

    public string SignParamName { get; set; } = "sign";
}