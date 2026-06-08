using System.Diagnostics;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Diagnostics;

using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApiSign.AspNetCore.Filters;

/// <summary>
/// MVC authorization filter that validates request signatures.
/// </summary>
public sealed class ApiSignAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ISignValidator _signValidator;
    private readonly IApiSignFailureResponseHandler _failureResponseHandler;

    /// <summary>
    /// MVC authorization filter that validates request signatures.
    /// </summary>
    public ApiSignAuthorizationFilter(ISignValidator signValidator,
        IApiSignFailureResponseHandler failureResponseHandler)
    {
        _signValidator = signValidator;
        _failureResponseHandler = failureResponseHandler;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Filters.OfType<IAllowAnonymousFilter>().Any())
        {
            return;
        }

        using var activity = ApiSignDiagnostics.ActivitySource.StartActivity("ApiSign.Validate");
        activity?.SetTag("validation_context", "filter");
        activity?.SetTag("request_path", context.HttpContext.Request.Path.ToString());

        var result = await _signValidator.ValidateAsync(context.HttpContext);
        if (!result.Succeeded)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            activity?.SetTag("validation_result", "failure");
            activity?.SetTag("failure_reason", result.FailureReason.ToString());
            activity?.SetTag("error_message", result.ErrorMessage);

            context.Result = new ApiSignFailureActionResult(_failureResponseHandler, result);
            return;
        }

        activity?.SetTag("validation_result", "success");
        activity?.SetTag("appId", result.AppId);
    }
}