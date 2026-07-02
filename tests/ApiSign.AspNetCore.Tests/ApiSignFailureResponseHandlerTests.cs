using System.Text;
using System.Text.Json;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Extensions;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiSign.AspNetCore.Tests;

public sealed class ApiSignFailureResponseHandlerTests
{
    [Fact]
    public async Task HandleAsync_WritesDefaultJsonPayloadAndStatusCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var validationResult = SignValidationResult.Fail(
            ApiSignFailureReason.InvalidSignature,
            "The request signature is invalid.");

        var handler = new DefaultApiSignFailureResponseHandler();

        await handler.HandleAsync(httpContext, validationResult, cancellationToken);

        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: cancellationToken);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal("InvalidSignature", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("The request signature is invalid.", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public void AddApiSignAuthentication_WithCustomFailureHandler_PreservesCustomRegistration()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IApiSignFailureResponseHandler, PlainTextFailureResponseHandler>();
        services.AddApiSignAuthentication();

        using var provider = services.BuildServiceProvider();

        var handler = provider.GetRequiredService<IApiSignFailureResponseHandler>();

        Assert.IsType<PlainTextFailureResponseHandler>(handler);
    }

    [Fact]
    public void AddApiSignAuthentication_WithNonceExpiryShorterThanTimestampWindow_ThrowsOnAccess()
    {
        var services = new ServiceCollection();
        services.AddApiSignAuthentication(options =>
        {
            options.EnableNonce = true;
            options.TimestampDisparitySeconds = 900;
            options.NonceExpireSeconds = 60;
        });

        using var provider = services.BuildServiceProvider();
        var optionsAccessor = provider.GetRequiredService<IOptions<ApiSignOptions>>();

        var ex = Assert.Throws<OptionsValidationException>(() => optionsAccessor.Value);
        Assert.Contains("NonceExpireSeconds", ex.Message);
    }

    [Fact]
    public void AddApiSignAuthentication_WithNonceDisabled_AllowsShorterNonceExpiry()
    {
        var services = new ServiceCollection();
        services.AddApiSignAuthentication(options =>
        {
            options.EnableNonce = false;
            options.TimestampDisparitySeconds = 900;
            options.NonceExpireSeconds = 60;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiSignOptions>>().Value;

        Assert.False(options.EnableNonce);
        Assert.Equal(60, options.NonceExpireSeconds);
    }

    private sealed class PlainTextFailureResponseHandler : IApiSignFailureResponseHandler
    {
        public async Task HandleAsync(
            HttpContext httpContext,
            SignValidationResult validationResult,
            CancellationToken cancellationToken = default)
        {
            httpContext.Response.StatusCode = StatusCodes.Status418ImATeapot;
            httpContext.Response.ContentType = "text/plain; charset=utf-8";
            await httpContext.Response.WriteAsync(
                $"sign validation failed: {validationResult.FailureReason}",
                Encoding.UTF8,
                cancellationToken);
        }
    }
}