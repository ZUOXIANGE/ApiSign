using ApiSign.AspNetCore.Abstractions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiSign.AspNetCore.Middleware;

/// <summary>
/// Global middleware for API signature validation.
/// </summary>
public sealed class ApiSignMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiSignMiddleware> _logger;
    private readonly IReadOnlyCollection<PathString> _excludedPaths;

    public ApiSignMiddleware(
        RequestDelegate next,
        ILogger<ApiSignMiddleware> logger,
        PathString[]? excludedPaths = null)
    {
        _next = next;
        _logger = logger;
        _excludedPaths = excludedPaths ?? [];
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISignValidator signValidator,
        IApiSignFailureResponseHandler failureResponseHandler)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ShouldSkip(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var result = await signValidator.ValidateAsync(context);
        if (!result.Succeeded)
        {
            _logger.LogWarning("ApiSign validation failed for path {Path}. Reason: {Reason}.", context.Request.Path, result.FailureReason);
            await failureResponseHandler.HandleAsync(context, result, context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private bool ShouldSkip(PathString requestPath)
        => _excludedPaths.Any(path => requestPath.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
}