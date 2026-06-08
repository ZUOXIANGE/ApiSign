using System.Diagnostics;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Diagnostics;

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

        using var activity = ApiSignDiagnostics.ActivitySource.StartActivity("ApiSign.Validate");
        activity?.SetTag("validation_context", "middleware");
        activity?.SetTag("request_path", context.Request.Path.ToString());

        var result = await signValidator.ValidateAsync(context);
        if (!result.Succeeded)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            activity?.SetTag("validation_result", "failure");
            activity?.SetTag("failure_reason", result.FailureReason.ToString());
            activity?.SetTag("error_message", result.ErrorMessage);

            _logger.LogWarning("ApiSign validation failed for path {Path}. Reason: {Reason}.", context.Request.Path, result.FailureReason);
            await failureResponseHandler.HandleAsync(context, result, context.RequestAborted);
            return;
        }

        activity?.SetTag("validation_result", "success");
        activity?.SetTag("appId", result.AppId);

        await _next(context);
    }

    private bool ShouldSkip(PathString requestPath)
        => _excludedPaths.Any(path => requestPath.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
}