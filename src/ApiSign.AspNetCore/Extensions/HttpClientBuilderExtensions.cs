using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Handlers;
using ApiSign.AspNetCore.Models;

using Microsoft.Extensions.DependencyInjection;

namespace ApiSign.AspNetCore.Extensions;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddApiSignMessageHandler(
        this IHttpClientBuilder builder,
        string appId)
        => builder.AddApiSignMessageHandler(appId, configure: null);

    public static IHttpClientBuilder AddApiSignMessageHandler(
        this IHttpClientBuilder builder,
        string appId,
        Action<ApiSignHttpMessageHandlerOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var options = new ApiSignHttpMessageHandlerOptions { AppId = appId };
        configure?.Invoke(options);

        builder.AddHttpMessageHandler(sp =>
            new ApiSignHttpMessageHandler(
                options,
                sp.GetRequiredService<IAppSecretProvider>(),
                sp.GetRequiredService<ISignatureCalculator>(),
                sp.GetRequiredService<TimeProvider>()));

        return builder;
    }
}