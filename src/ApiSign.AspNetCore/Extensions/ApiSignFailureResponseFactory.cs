using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;

namespace ApiSign.AspNetCore.Extensions;

internal static class ApiSignFailureResponseFactory
{
    public static int GetStatusCode(ApiSignFailureReason failureReason)
        => failureReason switch
        {
            ApiSignFailureReason.MissingParameters => StatusCodes.Status400BadRequest,
            ApiSignFailureReason.InvalidTimestamp => StatusCodes.Status401Unauthorized,
            ApiSignFailureReason.AppNotFound => StatusCodes.Status401Unauthorized,
            ApiSignFailureReason.AppDisabled => StatusCodes.Status403Forbidden,
            ApiSignFailureReason.ReplayAttack => StatusCodes.Status409Conflict,
            ApiSignFailureReason.InvalidSignature => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status401Unauthorized,
        };

    public static object CreateBody(SignValidationResult result)
        => new
        {
            code = result.FailureReason.ToString(),
            message = result.ErrorMessage,
        };
}