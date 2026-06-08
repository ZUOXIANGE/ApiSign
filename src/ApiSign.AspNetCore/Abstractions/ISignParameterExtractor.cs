using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;

namespace ApiSign.AspNetCore.Abstractions;

/// <summary>
/// Extracts signature related parameters from the current request.
/// </summary>
public interface ISignParameterExtractor
{
    /// <summary>
    /// Extracts signing parameters from the request.
    /// </summary>
    Task<SignParameters> ExtractAsync(HttpRequest request);
}