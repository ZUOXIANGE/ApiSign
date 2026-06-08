using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Filters;
using ApiSign.AspNetCore.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApiSign.AspNetCore.Extensions;

/// <summary>
/// Dependency injection helpers for ApiSign.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiSignAuthentication(this IServiceCollection services)
        => services.AddApiSignAuthentication(configure: null);

    public static IServiceCollection AddApiSignAuthentication(this IServiceCollection services, Action<ApiSignOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();
        services.AddOptions<ApiSignOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<INonceStore, DefaultNonceStore>();
        services.TryAddSingleton<ISignParameterExtractor, DefaultSignParameterExtractor>();
        services.TryAddSingleton<ISignatureCalculator, SignatureCalculator>();
        services.TryAddSingleton<IApiSignFailureResponseHandler, DefaultApiSignFailureResponseHandler>();
        services.TryAddScoped<ISignValidator, SignValidator>();
        services.TryAddScoped<ApiSignAuthorizationFilter>();

        return services;
    }
}