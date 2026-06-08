using ApiSign.AspNetCore.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ApiSign.AspNetCore.Extensions;

/// <summary>
/// Application builder helpers for ApiSign.
/// </summary>
public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseApiSignAuthentication(this IApplicationBuilder app)
        => app.UseApiSignAuthentication(excludedPaths: null);

    public static IApplicationBuilder UseApiSignAuthentication(this IApplicationBuilder app, IEnumerable<string>? excludedPaths)
    {
        ArgumentNullException.ThrowIfNull(app);

        var normalizedPaths = excludedPaths?
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => new PathString(path.StartsWith('/') ? path : $"/{path}"))
            .ToArray() ?? [];

        return app.UseMiddleware<ApiSignMiddleware>(normalizedPaths);
    }
}