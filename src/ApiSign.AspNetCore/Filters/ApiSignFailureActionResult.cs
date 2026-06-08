using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Mvc;

namespace ApiSign.AspNetCore.Filters;

internal sealed class ApiSignFailureActionResult : IActionResult
{
    private readonly IApiSignFailureResponseHandler _failureResponseHandler;
    private readonly SignValidationResult _validationResult;

    public ApiSignFailureActionResult(IApiSignFailureResponseHandler failureResponseHandler,
        SignValidationResult validationResult)
    {
        _failureResponseHandler = failureResponseHandler;
        _validationResult = validationResult;
    }

    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _failureResponseHandler.HandleAsync(context.HttpContext, _validationResult, context.HttpContext.RequestAborted);
    }
}