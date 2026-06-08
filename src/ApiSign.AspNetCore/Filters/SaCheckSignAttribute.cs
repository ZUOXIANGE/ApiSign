using Microsoft.AspNetCore.Mvc;

namespace ApiSign.AspNetCore.Filters;

/// <summary>
/// Applies API signature validation to controllers or actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SaCheckSignAttribute : TypeFilterAttribute
{
    public SaCheckSignAttribute()
        : base(typeof(ApiSignAuthorizationFilter))
    {
    }
}