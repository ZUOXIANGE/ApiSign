using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Extensions;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;

namespace ApiSign.AspNetCore.Core;

/// <summary>
/// Writes the default JSON response when signature validation fails.
/// </summary>
public sealed class DefaultApiSignFailureResponseHandler : IApiSignFailureResponseHandler
{
    public Task HandleAsync(
        HttpContext httpContext,
        SignValidationResult validationResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(validationResult);

        httpContext.Response.StatusCode = ApiSignFailureResponseFactory.GetStatusCode(validationResult.FailureReason);

        return httpContext.Response.WriteAsJsonAsync(
            ApiSignFailureResponseFactory.CreateBody(validationResult),
            cancellationToken);
    }
}