using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.WebUtilities;

namespace ApiSign.AspNetCore.Handlers;

public sealed class ApiSignHttpMessageHandler : DelegatingHandler
{
    private readonly ApiSignHttpMessageHandlerOptions _options;
    private readonly IAppSecretProvider _appSecretProvider;
    private readonly ISignatureCalculator _signatureCalculator;
    private readonly TimeProvider _timeProvider;

    public ApiSignHttpMessageHandler(
        ApiSignHttpMessageHandlerOptions options,
        IAppSecretProvider appSecretProvider,
        ISignatureCalculator signatureCalculator,
        TimeProvider timeProvider)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _appSecretProvider = appSecretProvider ?? throw new ArgumentNullException(nameof(appSecretProvider));
        _signatureCalculator = signatureCalculator ?? throw new ArgumentNullException(nameof(signatureCalculator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appSecret = await _appSecretProvider.GetAppSecretAsync(_options.AppId);
        if (appSecret is null)
        {
            throw new InvalidOperationException($"AppSecret not found for appId '{_options.AppId}'.");
        }

        if (!appSecret.IsEnabled)
        {
            throw new InvalidOperationException($"AppSecret is disabled for appId '{_options.AppId}'.");
        }

        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");

        var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddQueryParams(request, collected);

        byte[]? contentBytes = null;
        if (request.Content is not null)
        {
            contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            AddBodyParams(request.Content, contentBytes, collected);
        }

        if (_options.StrictMode)
        {
            RemoveSignatureParameters(collected);
        }

        var signParams = new SignParameters
        {
            AppId = _options.AppId,
            Nonce = nonce,
            Timestamp = timestamp,
            OtherParams = collected,
        };

        var algorithm = _options.Algorithm ?? appSecret.Algorithm;
        var sign = _signatureCalculator.Calculate(signParams, appSecret.SecretKey, algorithm);

        request.Headers.Remove(_options.AppIdHeaderName);
        request.Headers.Remove(_options.NonceHeaderName);
        request.Headers.Remove(_options.TimestampHeaderName);
        request.Headers.Remove(_options.SignHeaderName);

        request.Headers.Add(_options.AppIdHeaderName, _options.AppId);
        request.Headers.Add(_options.NonceHeaderName, nonce);
        request.Headers.Add(_options.TimestampHeaderName, timestamp.ToString());
        request.Headers.Add(_options.SignHeaderName, sign);

        if (contentBytes is not null && request.Content is not null)
        {
            var newContent = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
            {
                newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Content = newContent;
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private void AddQueryParams(HttpRequestMessage request, IDictionary<string, string> target)
    {
        var queryString = request.RequestUri?.Query;
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return;
        }

        var parsed = QueryHelpers.ParseQuery(queryString);
        foreach (var pair in parsed)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value.ToString()))
            {
                target[pair.Key] = pair.Value.ToString();
            }
        }
    }

    private static void AddBodyParams(HttpContent content, byte[] contentBytes, IDictionary<string, string> target)
    {
        var contentType = content.Headers.ContentType;
        if (contentType is null)
        {
            return;
        }

        var mediaType = contentType.MediaType;
        if (mediaType is null)
        {
            return;
        }

        if (mediaType.Contains("form", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            AddFormBodyParams(contentBytes, target);
            return;
        }

        if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            AddJsonBodyParams(contentBytes, target);
        }
    }

    private static void AddFormBodyParams(byte[] contentBytes, IDictionary<string, string> target)
    {
        var bodyString = Encoding.UTF8.GetString(contentBytes);
        var parsed = QueryHelpers.ParseQuery(bodyString);
        foreach (var pair in parsed)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value.ToString()))
            {
                target[pair.Key] = pair.Value.ToString();
            }
        }
    }

    private static void AddJsonBodyParams(byte[] contentBytes, IDictionary<string, string> target)
    {
        if (contentBytes.Length == 0)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(contentBytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = ConvertJsonValue(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target[property.Name] = value;
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    private void RemoveSignatureParameters(IDictionary<string, string> target)
    {
        target.Remove(_options.AppIdHeaderName);
        target.Remove(_options.NonceHeaderName);
        target.Remove(_options.TimestampHeaderName);
        target.Remove(_options.SignHeaderName);
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