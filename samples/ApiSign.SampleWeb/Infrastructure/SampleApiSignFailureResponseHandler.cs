using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;

namespace ApiSign.SampleWeb.Infrastructure;

public sealed class SampleApiSignFailureResponseHandler : IApiSignFailureResponseHandler
{
    public Task HandleAsync(
        HttpContext httpContext,
        SignValidationResult validationResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(validationResult);

        httpContext.Response.StatusCode = GetStatusCode(validationResult.FailureReason);

        return httpContext.Response.WriteAsJsonAsync(
            new
            {
                success = false,
                errorCode = validationResult.FailureReason.ToString(),
                errorMessage = validationResult.ErrorMessage,
                traceId = httpContext.TraceIdentifier,
                path = httpContext.Request.Path.Value,
                timestamp = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static int GetStatusCode(ApiSignFailureReason failureReason)
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
}