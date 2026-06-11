using System.Net;
using System.Net.Http.Json;

using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Handlers;
using ApiSign.AspNetCore.Models;

namespace ApiSign.AspNetCore.Tests;

public sealed class ApiSignHttpMessageHandlerTests
{
    private static readonly AppSecretInfo TestAppSecret = new()
    {
        AppId = "test-app",
        SecretKey = "test-secret",
        IsEnabled = true,
        Algorithm = SignAlgorithm.HMACSHA256,
    };

    [Fact]
    public async Task SendAsync_AddsSignatureHeadersToRequest()
    {
        var invoker = CreateInvoker(out var innerHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(innerHandler.CapturedRequest);

        var captured = innerHandler.CapturedRequest;
        Assert.True(captured.Headers.Contains("appId"));
        Assert.True(captured.Headers.Contains("nonce"));
        Assert.True(captured.Headers.Contains("timestamp"));
        Assert.True(captured.Headers.Contains("sign"));

        Assert.Equal("test-app", captured.Headers.GetValues("appId").First());
    }

    [Fact]
    public async Task SendAsync_Throws_WhenAppSecretNotFound()
    {
        var appSecretProvider = new FakeAppSecretProvider(null);
        var handler = new ApiSignHttpMessageHandler(
            new ApiSignHttpMessageHandlerOptions { AppId = "unknown-app" },
            appSecretProvider,
            new SignatureCalculator(),
            TimeProvider.System);
        var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.SendAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("unknown-app", ex.Message);
    }

    [Fact]
    public async Task SendAsync_Throws_WhenAppSecretDisabled()
    {
        var appSecretProvider = new FakeAppSecretProvider(new AppSecretInfo
        {
            AppId = "test-app",
            SecretKey = "test-secret",
            IsEnabled = false,
        });
        var handler = new ApiSignHttpMessageHandler(
            new ApiSignHttpMessageHandlerOptions { AppId = "test-app" },
            appSecretProvider,
            new SignatureCalculator(),
            TimeProvider.System);
        var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.SendAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_StrictMode_ExcludesBodySignatureParamsFromCanonical()
    {
        var invoker = CreateInvoker(out var innerHandler, strictMode: true);
        var payload = new { appId = "should-be-excluded", orderId = "ORD-001", amount = 100 };
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://example.com/api/test")
        {
            Content = JsonContent.Create(payload),
        };

        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);
        var signHeader = captured.Headers.GetValues("sign").First();
        var nonceHeader = captured.Headers.GetValues("nonce").First();
        var timestampHeader = captured.Headers.GetValues("timestamp").First();

        var calculator = new SignatureCalculator();
        var expectedSig = calculator.Calculate(
            new SignParameters
            {
                AppId = "test-app",
                Nonce = nonceHeader,
                Timestamp = long.Parse(timestampHeader),
                OtherParams = new Dictionary<string, string>
                {
                    ["orderId"] = "ORD-001",
                    ["amount"] = "100",
                },
            },
            TestAppSecret.SecretKey,
            TestAppSecret.Algorithm);

        Assert.Equal(expectedSig, signHeader);
    }

    [Fact]
    public async Task SendAsync_NonStrictMode_IncludesAllParams()
    {
        var invoker = CreateInvoker(out var innerHandler, strictMode: false);
        var payload = new { orderId = "ORD-002", amount = 200 };
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://example.com/api/test")
        {
            Content = JsonContent.Create(payload),
        };

        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);
        var signHeader = captured.Headers.GetValues("sign").First();
        var nonceHeader = captured.Headers.GetValues("nonce").First();
        var timestampHeader = captured.Headers.GetValues("timestamp").First();

        var calculator = new SignatureCalculator();
        var expectedSig = calculator.Calculate(
            new SignParameters
            {
                AppId = "test-app",
                Nonce = nonceHeader,
                Timestamp = long.Parse(timestampHeader),
                OtherParams = new Dictionary<string, string>
                {
                    ["orderId"] = "ORD-002",
                    ["amount"] = "200",
                },
            },
            TestAppSecret.SecretKey,
            TestAppSecret.Algorithm);

        Assert.Equal(expectedSig, signHeader);
    }

    [Fact]
    public async Task SendAsync_IncludesQueryParams()
    {
        var invoker = CreateInvoker(out var innerHandler, strictMode: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test?foo=bar&baz=qux");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);
        var signHeader = captured.Headers.GetValues("sign").First();
        var nonceHeader = captured.Headers.GetValues("nonce").First();
        var timestampHeader = captured.Headers.GetValues("timestamp").First();

        var calculator = new SignatureCalculator();
        var expectedSig = calculator.Calculate(
            new SignParameters
            {
                AppId = "test-app",
                Nonce = nonceHeader,
                Timestamp = long.Parse(timestampHeader),
                OtherParams = new Dictionary<string, string>
                {
                    ["baz"] = "qux",
                    ["foo"] = "bar",
                },
            },
            TestAppSecret.SecretKey,
            TestAppSecret.Algorithm);

        Assert.Equal(expectedSig, signHeader);
    }

    [Fact]
    public async Task SendAsync_PreservesOriginalContent()
    {
        var invoker = CreateInvoker(out var innerHandler);
        var payload = new { name = "test", value = 42 };
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://example.com/api/test")
        {
            Content = JsonContent.Create(payload),
        };

        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);
        Assert.NotNull(captured.Content);

        var body = await captured.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("test", body);
        Assert.Contains("42", body);
    }

    [Fact]
    public async Task SendAsync_SupportsCustomHeaderNames()
    {
        var options = new ApiSignHttpMessageHandlerOptions
        {
            AppId = "test-app",
            AppIdHeaderName = "X-AppId",
            NonceHeaderName = "X-Nonce",
            TimestampHeaderName = "X-Timestamp",
            SignHeaderName = "X-Sign",
        };

        var invoker = CreateInvokerWithOptions(options, out var innerHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);
        Assert.True(captured.Headers.Contains("X-AppId"));
        Assert.True(captured.Headers.Contains("X-Nonce"));
        Assert.True(captured.Headers.Contains("X-Timestamp"));
        Assert.True(captured.Headers.Contains("X-Sign"));
        Assert.False(captured.Headers.Contains("appId"));
    }

    [Fact]
    public async Task SendAsync_OverridesExistingSignatureHeaders()
    {
        var invoker = CreateInvoker(out var innerHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test");
        request.Headers.Add("appId", "old-app-id");
        request.Headers.Add("nonce", "old-nonce");
        request.Headers.Add("timestamp", "12345");
        request.Headers.Add("sign", "old-sign");

        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);

        var appIdValues = captured.Headers.GetValues("appId").ToList();
        Assert.Single(appIdValues);
        Assert.Equal("test-app", appIdValues[0]);
    }

    [Fact]
    public async Task SendAsync_OverridesAlgorithmFromOptions()
    {
        var options = new ApiSignHttpMessageHandlerOptions
        {
            AppId = "test-app",
            Algorithm = SignAlgorithm.SHA256,
        };

        var invoker = CreateInvokerWithOptions(options, out var innerHandler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com/api/test");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var captured = innerHandler.CapturedRequest;
        Assert.NotNull(captured);
        var signHeader = captured.Headers.GetValues("sign").First();
        var nonceHeader = captured.Headers.GetValues("nonce").First();
        var timestampHeader = captured.Headers.GetValues("timestamp").First();

        var calculator = new SignatureCalculator();
        var expectedSig = calculator.Calculate(
            new SignParameters
            {
                AppId = "test-app",
                Nonce = nonceHeader,
                Timestamp = long.Parse(timestampHeader),
            },
            TestAppSecret.SecretKey,
            SignAlgorithm.SHA256);

        Assert.Equal(expectedSig, signHeader);
    }

    private static HttpMessageInvoker CreateInvoker(
        out FakeHttpMessageHandler innerHandler,
        bool strictMode = true)
    {
        var options = new ApiSignHttpMessageHandlerOptions
        {
            AppId = "test-app",
            StrictMode = strictMode,
        };

        return CreateInvokerWithOptions(options, out innerHandler);
    }

    private static HttpMessageInvoker CreateInvokerWithOptions(
        ApiSignHttpMessageHandlerOptions options,
        out FakeHttpMessageHandler innerHandler)
    {
        innerHandler = new FakeHttpMessageHandler();
        var appSecretProvider = new FakeAppSecretProvider(TestAppSecret);

        var handler = new ApiSignHttpMessageHandler(
            options,
            appSecretProvider,
            new SignatureCalculator(),
            TimeProvider.System)
        {
            InnerHandler = innerHandler,
        };

        return new HttpMessageInvoker(handler);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeAppSecretProvider : IAppSecretProvider
    {
        private readonly AppSecretInfo? _appSecret;

        public FakeAppSecretProvider(AppSecretInfo? appSecret)
        {
            _appSecret = appSecret;
        }

        public Task<AppSecretInfo?> GetAppSecretAsync(string appId)
            => Task.FromResult(_appSecret);
    }
}