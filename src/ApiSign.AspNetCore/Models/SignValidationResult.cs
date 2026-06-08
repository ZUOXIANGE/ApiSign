namespace ApiSign.AspNetCore.Models;

/// <summary>
/// Represents the result of request signature validation.
/// </summary>
public sealed class SignValidationResult
{
    private SignValidationResult(bool succeeded, ApiSignFailureReason failureReason, string? errorMessage, string? appId)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        ErrorMessage = errorMessage;
        AppId = appId;
    }

    public bool Succeeded { get; }

    public ApiSignFailureReason FailureReason { get; }

    public string? ErrorMessage { get; }

    public string? AppId { get; }

    public static SignValidationResult Success(string? appId) => new(true, ApiSignFailureReason.None, null, appId);

    public static SignValidationResult Fail(ApiSignFailureReason failureReason, string errorMessage) =>
        new(false, failureReason, errorMessage, null);
}