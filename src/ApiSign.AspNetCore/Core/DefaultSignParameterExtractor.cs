using System.Text.Json;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ApiSign.AspNetCore.Core;

/// <summary>
/// Default request parameter extractor.
/// </summary>
public sealed class DefaultSignParameterExtractor : ISignParameterExtractor
{
    private readonly ApiSignOptions _options;
    private readonly ILogger<DefaultSignParameterExtractor> _logger;

    /// <summary>
    /// Default request parameter extractor.
    /// </summary>
    public DefaultSignParameterExtractor(IOptions<ApiSignOptions> options, ILogger<DefaultSignParameterExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SignParameters> ExtractAsync(HttpRequest request)
    {
        var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddFromQuery(request, collected);
        await AddFromBodyAsync(request, collected);
        if (_options.StrictMode)
        {
            RemoveSignatureParameters(collected);
        }

        AddSignatureParametersFromHeaders(request, collected, overwrite: _options.StrictMode);

        return new SignParameters
        {
            AppId = TryExtract(collected, _options.AppIdParamName),
            Nonce = TryExtract(collected, _options.NonceParamName),
            Timestamp = TryParseInt64(TryExtract(collected, _options.TimestampParamName)),
            Sign = TryExtract(collected, _options.SignParamName),
            OtherParams = collected,
        };
    }

    private static string? TryExtract(IDictionary<string, string> values, string key)
    {
        if (values.Remove(key, out var value))
        {
            return value;
        }

        return null;
    }

    private static long? TryParseInt64(string? value)
        => long.TryParse(value, out var result) ? result : null;

    private static void AddFromQuery(HttpRequest request, IDictionary<string, string> target)
    {
        foreach (var pair in request.Query)
        {
            TryAdd(target, pair.Key, pair.Value, overwrite: false);
        }
    }

    private async Task AddFromBodyAsync(HttpRequest request, IDictionary<string, string> target)
    {
        if (!HttpMethods.IsPost(request.Method) &&
            !HttpMethods.IsPut(request.Method) &&
            !HttpMethods.IsPatch(request.Method) &&
            !HttpMethods.IsDelete(request.Method))
        {
            return;
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            foreach (var pair in form)
            {
                TryAdd(target, pair.Key, pair.Value, overwrite: false);
            }

            return;
        }

        if (request.ContentLength is null or 0)
        {
            return;
        }

        if (!request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            return;
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                TryAdd(target, property.Name, ConvertJsonValue(property.Value), overwrite: false);
            }
        }
        catch (JsonException ex)
        {
            if (_options.StrictMode)
            {
                throw;
            }

            _logger.LogWarning(ex, "Failed to parse JSON body for signature parameters. Body will be ignored in non-strict mode.");
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private void AddSignatureParametersFromHeaders(HttpRequest request, IDictionary<string, string> target, bool overwrite)
    {
        AddFromHeader(request, target, _options.AppIdParamName, overwrite);
        AddFromHeader(request, target, _options.NonceParamName, overwrite);
        AddFromHeader(request, target, _options.TimestampParamName, overwrite);
        AddFromHeader(request, target, _options.SignParamName, overwrite);
    }

    private void RemoveSignatureParameters(IDictionary<string, string> target)
    {
        target.Remove(_options.AppIdParamName);
        target.Remove(_options.NonceParamName);
        target.Remove(_options.TimestampParamName);
        target.Remove(_options.SignParamName);
    }

    private void AddFromHeader(HttpRequest request, IDictionary<string, string> target, string headerName, bool overwrite)
    {
        if (request.Headers.TryGetValue(headerName, out var headerValue))
        {
            TryAdd(target, headerName, headerValue, overwrite);
        }
    }

    private static void TryAdd(IDictionary<string, string> target, string key, StringValues values, bool overwrite)
        => TryAdd(target, key, values.ToString(), overwrite);

    private static void TryAdd(IDictionary<string, string> target, string key, string? value, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (overwrite || !target.ContainsKey(key))
        {
            target[key] = value;
        }
    }

    private static string ConvertJsonValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText(),
        };
}