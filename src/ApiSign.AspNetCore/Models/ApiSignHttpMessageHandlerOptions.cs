namespace ApiSign.AspNetCore.Models;

public sealed class ApiSignHttpMessageHandlerOptions
{
    public string AppId { get; set; } = string.Empty;

    public SignAlgorithm? Algorithm { get; set; }

    public bool StrictMode { get; set; } = true;

    public string AppIdHeaderName { get; set; } = "appId";

    public string NonceHeaderName { get; set; } = "nonce";

    public string TimestampHeaderName { get; set; } = "timestamp";

    public string SignHeaderName { get; set; } = "sign";
}