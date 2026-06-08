using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Constants;
using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiSign.AspNetCore.Tests;

public sealed class SignValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WithValidRequest_SucceedsAndSupportsDoubleValidation()
    {
        var now = DateTimeOffset.Parse("2026-06-05T12:00:00Z");
        var calculator = new SignatureCalculator();
        var parameters = new SignParameters
        {
            AppId = "demo-app",
            Nonce = "nonce-001",
            Timestamp = now.ToUnixTimeSeconds(),
            OtherParams = new Dictionary<string, string>
            {
                ["amount"] = "12",
            },
        };
        parameters.Sign = calculator.Calculate(parameters, "secret-001", SignAlgorithm.HMACSHA256);

        var validator = new SignValidator(
            new StubExtractor(parameters),
            new StubAppSecretProvider(),
            new DefaultNonceStore(new MemoryCache(new MemoryCacheOptions())),
            calculator,
            Options.Create(new ApiSignOptions()),
            new FakeTimeProvider(now));

        var context = new DefaultHttpContext();

        var first = await validator.ValidateAsync(context);
        var second = await validator.ValidateAsync(context);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal("demo-app", context.Items[ApiSignConstants.AppIdItemKey]);
        Assert.Equal(true, context.Items[ApiSignConstants.ValidationSucceededItemKey]);
    }

    [Fact]
    public async Task ValidateAsync_ReusedNonce_Fails()
    {
        var now = DateTimeOffset.Parse("2026-06-05T12:00:00Z");
        var calculator = new SignatureCalculator();
        var parameters = new SignParameters
        {
            AppId = "demo-app",
            Nonce = "nonce-001",
            Timestamp = now.ToUnixTimeSeconds(),
        };
        parameters.Sign = calculator.Calculate(parameters, "secret-001", SignAlgorithm.HMACSHA256);

        var nonceStore = new DefaultNonceStore(new MemoryCache(new MemoryCacheOptions()));
        var validator = new SignValidator(
            new StubExtractor(parameters),
            new StubAppSecretProvider(),
            nonceStore,
            calculator,
            Options.Create(new ApiSignOptions()),
            new FakeTimeProvider(now));

        var first = await validator.ValidateAsync(new DefaultHttpContext());
        var second = await validator.ValidateAsync(new DefaultHttpContext());

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(ApiSignFailureReason.ReplayAttack, second.FailureReason);
    }

    private sealed class StubExtractor(SignParameters parameters) : ISignParameterExtractor
    {
        public Task<SignParameters> ExtractAsync(HttpRequest request) => Task.FromResult(parameters);
    }

    private sealed class StubAppSecretProvider : IAppSecretProvider
    {
        public Task<AppSecretInfo?> GetAppSecretAsync(string appId)
            => Task.FromResult<AppSecretInfo?>(new AppSecretInfo
            {
                AppId = appId,
                SecretKey = "secret-001",
                Algorithm = SignAlgorithm.HMACSHA256,
                IsEnabled = true,
            });
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}