using System.Security.Cryptography;
using System.Text;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

namespace ApiSign.AspNetCore.Core;

/// <summary>
/// Default signature calculator.
/// </summary>
public sealed class SignatureCalculator : ISignatureCalculator
{
    public string BuildCanonicalString(SignParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var items = new SortedDictionary<string, string>(StringComparer.Ordinal);
        Add(items, "appId", parameters.AppId);
        Add(items, "nonce", parameters.Nonce);
        Add(items, "timestamp", parameters.Timestamp?.ToString());

        foreach (var pair in parameters.OtherParams)
        {
            Add(items, pair.Key, pair.Value);
        }

        return string.Join("&", items.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    public string Calculate(SignParameters parameters, string secretKey, SignAlgorithm algorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        var canonical = BuildCanonicalString(parameters);
        var payload = algorithm is SignAlgorithm.MD5 or SignAlgorithm.SHA256
            ? $"{canonical}&key={secretKey}"
            : canonical;
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        return algorithm switch
        {
            SignAlgorithm.MD5 => ToHex(MD5.HashData(payloadBytes)),
            SignAlgorithm.SHA256 => ToHex(SHA256.HashData(payloadBytes)),
            SignAlgorithm.HMACSHA256 => ToHex(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), payloadBytes)),
            SignAlgorithm.HMACSHA512 => ToHex(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secretKey), payloadBytes)),
            _ => throw new NotSupportedException($"Unsupported algorithm: {algorithm}"),
        };
    }

    private static void Add(IDictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value;
        }
    }

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);
}