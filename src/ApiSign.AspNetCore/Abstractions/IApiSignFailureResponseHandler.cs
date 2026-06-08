using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;

namespace ApiSign.AspNetCore.Abstractions;

/// <summary>
/// Handles responses for failed API signature validation.
/// </summary>
public interface IApiSignFailureResponseHandler
{
    Task HandleAsync(
        HttpContext httpContext,
        SignValidationResult validationResult,
        CancellationToken cancellationToken = default);
}