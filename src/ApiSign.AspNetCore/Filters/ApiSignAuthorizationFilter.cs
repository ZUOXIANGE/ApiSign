using ApiSign.AspNetCore.Abstractions;

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

        var result = await _signValidator.ValidateAsync(context.HttpContext);
        if (!result.Succeeded)
        {
            context.Result = new ApiSignFailureActionResult(_failureResponseHandler, result);
        }
    }
}