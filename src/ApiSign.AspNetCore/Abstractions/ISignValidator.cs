using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;

namespace ApiSign.AspNetCore.Abstractions;

/// <summary>
/// Validates API signature requests.
/// </summary>
public interface ISignValidator
{
    /// <summary>
    /// Validates the current request.
    /// </summary>
    Task<SignValidationResult> ValidateAsync(HttpContext httpContext);
}