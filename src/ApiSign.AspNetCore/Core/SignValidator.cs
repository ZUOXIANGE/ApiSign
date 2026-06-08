using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Constants;
using ApiSign.AspNetCore.Diagnostics;
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

        var sw = Stopwatch.StartNew();
        using var activity = ApiSignDiagnostics.ActivitySource.StartActivity("ApiSign.Validate");

        if (!_options.Enabled)
        {
            RecordValidationMetrics(sw, true, ApiSignFailureReason.None);
            return SignValidationResult.Success(appId: null);
        }

        // Check if the request has already been validated.
        if (httpContext.Items.TryGetValue(ApiSignConstants.ValidationSucceededItemKey, out var alreadyValidated) &&
            alreadyValidated is true)
        {
            RecordValidationMetrics(sw, true, ApiSignFailureReason.None);
            return SignValidationResult.Success(httpContext.Items[ApiSignConstants.AppIdItemKey]?.ToString());
        }

        var signParameters = await _parameterExtractor.ExtractAsync(httpContext.Request);

        if (string.IsNullOrWhiteSpace(signParameters.AppId) ||
            string.IsNullOrWhiteSpace(signParameters.Sign) ||
            signParameters.Timestamp is null ||
            (_options.EnableNonce && string.IsNullOrWhiteSpace(signParameters.Nonce)))
        {
            const string message = "Missing required signing parameters.";
            SetFailureActivity(activity, ApiSignFailureReason.MissingParameters, message);
            RecordValidationMetrics(sw, false, ApiSignFailureReason.MissingParameters);
            return SignValidationResult.Fail(ApiSignFailureReason.MissingParameters, message);
        }

        var timestamp = signParameters.Timestamp.Value;
        var currentTimestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(currentTimestamp - timestamp) > _options.TimestampDisparitySeconds)
        {
            const string message = "The request timestamp is outside the allowed window.";
            SetFailureActivity(activity, ApiSignFailureReason.InvalidTimestamp, message);
            RecordValidationMetrics(sw, false, ApiSignFailureReason.InvalidTimestamp);
            return SignValidationResult.Fail(ApiSignFailureReason.InvalidTimestamp, message);
        }

        var appSecret = await _appSecretProvider.GetAppSecretAsync(signParameters.AppId);
        if (appSecret is null)
        {
            const string message = "The specified appId does not exist.";
            SetFailureActivity(activity, ApiSignFailureReason.AppNotFound, message);
            RecordValidationMetrics(sw, false, ApiSignFailureReason.AppNotFound);
            return SignValidationResult.Fail(ApiSignFailureReason.AppNotFound, message);
        }

        if (!appSecret.IsEnabled)
        {
            const string message = "The specified application is disabled.";
            SetFailureActivity(activity, ApiSignFailureReason.AppDisabled, message);
            RecordValidationMetrics(sw, false, ApiSignFailureReason.AppDisabled);
            return SignValidationResult.Fail(ApiSignFailureReason.AppDisabled, message);
        }

        if (_options.EnableNonce)
        {
            var nonceKey = BuildNonceKey(signParameters.AppId, signParameters.Nonce!, timestamp);
            if (await _nonceStore.ExistsAsync(nonceKey))
            {
                const string message = "The request nonce has already been used.";
                SetFailureActivity(activity, ApiSignFailureReason.ReplayAttack, message);
                RecordValidationMetrics(sw, false, ApiSignFailureReason.ReplayAttack);
                return SignValidationResult.Fail(ApiSignFailureReason.ReplayAttack, message);
            }

            await _nonceStore.SaveAsync(nonceKey, TimeSpan.FromSeconds(_options.NonceExpireSeconds));
        }

        var algorithm = appSecret.Algorithm == default ? _options.DefaultAlgorithm : appSecret.Algorithm;
        var canonicalString = _signatureCalculator.BuildCanonicalString(signParameters);
        var calculated = _signatureCalculator.Calculate(canonicalString, appSecret.SecretKey, algorithm);

        activity?.SetTag("apisign.canonical_string", canonicalString);
        activity?.SetTag("apisign.calculated_sign", calculated);
        activity?.SetTag("apisign.client_sign", signParameters.Sign);
        activity?.SetTag("appId", signParameters.AppId);
        activity?.SetTag("sign_algorithm", algorithm.ToString());

        if (!FixedTimeEquals(signParameters.Sign, calculated))
        {
            const string message = "The request signature is invalid.";
            SetFailureActivity(activity, ApiSignFailureReason.InvalidSignature, message);
            RecordValidationMetrics(sw, false, ApiSignFailureReason.InvalidSignature);
            return SignValidationResult.Fail(ApiSignFailureReason.InvalidSignature, message);
        }

        activity?.SetTag("validation_result", "success");
        RecordValidationMetrics(sw, true, ApiSignFailureReason.None);

        httpContext.Items[ApiSignConstants.AppIdItemKey] = signParameters.AppId;
        httpContext.Items[ApiSignConstants.ValidationSucceededItemKey] = true;
        return SignValidationResult.Success(signParameters.AppId);
    }

    private static void SetFailureActivity(Activity? activity, ApiSignFailureReason reason, string message)
    {
        activity?.SetStatus(ActivityStatusCode.Error, message);
        activity?.SetTag("validation_result", "failure");
        activity?.SetTag("failure_reason", reason.ToString());
        activity?.SetTag("error_message", message);
    }

    private static void RecordValidationMetrics(Stopwatch sw, bool succeeded, ApiSignFailureReason reason)
    {
        sw.Stop();
        var result = succeeded ? "success" : "failure";
        var tags = new TagList
        {
            { "result", result },
            { "reason", reason.ToString() }
        };
        ApiSignDiagnostics.ValidationDuration.Record(sw.Elapsed.TotalMilliseconds, tags);
        ApiSignDiagnostics.ValidationRequests.Add(1, tags);
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