namespace ApiSign.AspNetCore.Models;

/// <summary>
/// Represents all signature related parameters extracted from a request.
/// </summary>
public sealed class SignParameters
{
    public string? AppId { get; set; }

    public string? Nonce { get; set; }

    public long? Timestamp { get; set; }

    public string? Sign { get; set; }

    public Dictionary<string, string> OtherParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}