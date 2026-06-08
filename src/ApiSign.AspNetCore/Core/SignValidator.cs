using System.Security.Cryptography;
using System.Text;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Constants;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ApiSign.AspNetCore.Core;

/// <summary>
/// Default signature validation service.
/// </summary>
public sealed class SignValidator : ISignValidator
{
    private readonly ISignParameterExtractor _parameterExtractor;
    private readonly IAppSecretProvider _appSecretProvider;
    private readonly INonceStore _nonceStore;
    private readonly ISignatureCalculator _signatureCalculator;
    private readonly ApiSignOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Default signature validation service.
    /// </summary>
    public SignValidator(ISignParameterExtractor parameterExtractor,
        IAppSecretProvider appSecretProvider,
        INonceStore nonceStore,
        ISignatureCalculator signatureCalculator,
        IOptions<ApiSignOptions> options,
        TimeProvider timeProvider)
    {
        _parameterExtractor = parameterExtractor;
        _appSecretProvider = appSecretProvider;
        _nonceStore = nonceStore;
        _signatureCalculator = signatureCalculator;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<SignValidationResult> ValidateAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!_options.Enabled)
        {
            return SignValidationResult.Success(appId: null);
        }

        // Check if the request has already been validated.
        if (httpContext.Items.TryGetValue(ApiSignConstants.ValidationSucceededItemKey, out var alreadyValidated) &&
            alreadyValidated is true)
        {
            return SignValidationResult.Success(httpContext.Items[ApiSignConstants.AppIdItemKey]?.ToString());
        }

        var signParameters = await _parameterExtractor.ExtractAsync(httpContext.Request);

        if (string.IsNullOrWhiteSpace(signParameters.AppId) ||
            string.IsNullOrWhiteSpace(signParameters.Sign) ||
            signParameters.Timestamp is null ||
            (_options.EnableNonce && string.IsNullOrWhiteSpace(signParameters.Nonce)))
        {
            return SignValidationResult.Fail(ApiSignFailureReason.MissingParameters, "Missing required signing parameters.");
        }

        var timestamp = signParameters.Timestamp.Value;
        var currentTimestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(currentTimestamp - timestamp) > _options.TimestampDisparitySeconds)
        {
            return SignValidationResult.Fail(ApiSignFailureReason.InvalidTimestamp, "The request timestamp is outside the allowed window.");
        }

        var appSecret = await _appSecretProvider.GetAppSecretAsync(signParameters.AppId);
        if (appSecret is null)
        {
            return SignValidationResult.Fail(ApiSignFailureReason.AppNotFound, "The specified appId does not exist.");
        }

        if (!appSecret.IsEnabled)
        {
            return SignValidationResult.Fail(ApiSignFailureReason.AppDisabled, "The specified application is disabled.");
        }

        if (_options.EnableNonce)
        {
            var nonceKey = BuildNonceKey(signParameters.AppId, signParameters.Nonce!, timestamp);
            if (await _nonceStore.ExistsAsync(nonceKey))
            {
                return SignValidationResult.Fail(ApiSignFailureReason.ReplayAttack, "The request nonce has already been used.");
            }

            await _nonceStore.SaveAsync(nonceKey, TimeSpan.FromSeconds(_options.NonceExpireSeconds));
        }

        var algorithm = appSecret.Algorithm == default ? _options.DefaultAlgorithm : appSecret.Algorithm;
        var calculated = _signatureCalculator.Calculate(signParameters, appSecret.SecretKey, algorithm);
        if (!FixedTimeEquals(signParameters.Sign, calculated))
        {
            return SignValidationResult.Fail(ApiSignFailureReason.InvalidSignature, "The request signature is invalid.");
        }

        httpContext.Items[ApiSignConstants.AppIdItemKey] = signParameters.AppId;
        httpContext.Items[ApiSignConstants.ValidationSucceededItemKey] = true;
        return SignValidationResult.Success(signParameters.AppId);
    }

    private static string BuildNonceKey(string appId, string nonce, long timestamp)
        => $"{appId}:{nonce}:{timestamp}";

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left.ToUpperInvariant());
        var rightBytes = Encoding.UTF8.GetBytes(right.ToUpperInvariant());
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}