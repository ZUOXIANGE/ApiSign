using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiSign.AspNetCore.Tests;

public sealed class SampleWebIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SampleWebIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Transfer_WithValidSignature_ReturnsSuccessPayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        const string appId = "demo-app";
        const string secretKey = "secret-001";
        const string nonce = "integration-valid-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestBody = new
        {
            orderId = "ORD-IT-1001",
            amount = 99.50m,
            currency = "CNY",
        };

        using var request = await CreateSignedTransferRequestMessageAsync(
            appId,
            nonce,
            timestamp,
            secretKey,
            requestBody,
            strictMode: false);
        using var response = await client.SendAsync(request, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status code {(int)response.StatusCode} ({response.StatusCode}). Body: {responseContent}");

        var payload = JsonSerializer.Deserialize<JsonElement>(responseContent);

        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal(appId, payload.GetProperty("appId").GetString());
        Assert.Equal(requestBody.orderId, payload.GetProperty("orderId").GetString());
        Assert.Equal(99.50m, payload.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Transfer_WithoutSign_ReturnsCustomFailurePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        const string appId = "demo-app";
        const string nonce = "integration-missing-sign-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestUri = $"/api/payment/transfer?appId={appId}&nonce={nonce}&timestamp={timestamp}";
        var requestBody = new
        {
            orderId = "ORD-IT-1002",
            amount = 50.00m,
            currency = "CNY",
        };

        using var response = await client.PostAsJsonAsync(requestUri, requestBody, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("MissingParameters", payload.GetProperty("errorCode").GetString());
        Assert.Equal("Missing required signing parameters.", payload.GetProperty("errorMessage").GetString());
        Assert.Equal("/api/payment/transfer", payload.GetProperty("path").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.Equal(JsonValueKind.String, payload.GetProperty("timestamp").ValueKind);
    }

    [Fact]
    public async Task Transfer_WithStrictModeAndHeaderSignature_ReturnsSuccessPayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateStrictModeFactory();
        using var client = factory.CreateClient();

        const string appId = "demo-app";
        const string secretKey = "secret-001";
        const string nonce = "integration-strict-header-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestBody = new
        {
            orderId = "ORD-IT-2001",
            amount = 88.80m,
            currency = "CNY",
        };

        using var request = await CreateSignedTransferRequestMessageAsync(
            appId,
            nonce,
            timestamp,
            secretKey,
            requestBody,
            strictMode: true);

        using var response = await client.SendAsync(request, cancellationToken);
        var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status code {(int)response.StatusCode} ({response.StatusCode}). Body: {payloadText}");

        var payload = JsonSerializer.Deserialize<JsonElement>(payloadText);
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal(appId, payload.GetProperty("appId").GetString());
        Assert.Equal(requestBody.orderId, payload.GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task Transfer_WithStrictModeAndQuerySignature_ReturnsMissingParameters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateStrictModeFactory();
        using var client = factory.CreateClient();

        const string appId = "demo-app";
        const string secretKey = "secret-001";
        const string nonce = "integration-strict-query-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestBody = new
        {
            orderId = "ORD-IT-2002",
            amount = 66.60m,
            currency = "CNY",
        };

        using var request = await CreateSignedTransferRequestMessageAsync(
            appId,
            nonce,
            timestamp,
            secretKey,
            requestBody,
            strictMode: false);

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("MissingParameters", payload.GetProperty("errorCode").GetString());
        Assert.Equal("Missing required signing parameters.", payload.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task Transfer_WithStrictModeAndMissingTimestampHeader_ReturnsMissingParameters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateStrictModeFactory();
        using var client = factory.CreateClient();

        const string appId = "demo-app";
        const string secretKey = "secret-001";
        const string nonce = "integration-strict-missing-header-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestBody = new
        {
            orderId = "ORD-IT-2003",
            amount = 22.20m,
            currency = "CNY",
        };

        using var request = await CreateSignedTransferRequestMessageAsync(
            appId,
            nonce,
            timestamp,
            secretKey,
            requestBody,
            strictMode: true);
        request.Headers.Remove("timestamp");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("MissingParameters", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Transfer_WithStrictModeAndConflictingQuerySignatureParameters_UsesHeaderValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateStrictModeFactory();
        using var client = factory.CreateClient();

        const string appId = "demo-app";
        const string secretKey = "secret-001";
        const string nonce = "integration-strict-conflict-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestBody = new
        {
            orderId = "ORD-IT-2004",
            amount = 33.30m,
            currency = "CNY",
        };

        using var request = await CreateSignedTransferRequestMessageAsync(
            appId,
            nonce,
            timestamp,
            secretKey,
            requestBody,
            strictMode: true,
            conflictingQuerySignParameters: new Dictionary<string, string>
            {
                ["appId"] = "evil-app",
                ["nonce"] = "evil-nonce",
                ["timestamp"] = "1",
                ["sign"] = "INVALIDSIGNATURE",
            });

        using var response = await client.SendAsync(request, cancellationToken);
        var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status code {(int)response.StatusCode} ({response.StatusCode}). Body: {payloadText}");

        var payload = JsonSerializer.Deserialize<JsonElement>(payloadText);
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal(appId, payload.GetProperty("appId").GetString());
    }

    [Fact]
    public async Task Transfer_WithStrictModeAndBusinessParametersFromQueryAndBody_ReturnsSuccessPayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateStrictModeFactory();
        using var client = factory.CreateClient();

        const string appId = "demo-app";
        const string secretKey = "secret-001";
        const string nonce = "integration-strict-mixed-business-001";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestBody = new
        {
            orderId = "ORD-IT-2005",
            amount = 44.40m,
            currency = "CNY",
        };

        using var request = await CreateSignedTransferRequestMessageAsync(
            appId,
            nonce,
            timestamp,
            secretKey,
            requestBody,
            strictMode: true,
            businessQueryParameters: new Dictionary<string, string>
            {
                ["channel"] = "mobile-app",
                ["merchantNo"] = "MCH-1001",
            });

        using var response = await client.SendAsync(request, cancellationToken);
        var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected status code {(int)response.StatusCode} ({response.StatusCode}). Body: {payloadText}");

        var payload = JsonSerializer.Deserialize<JsonElement>(payloadText);
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal(appId, payload.GetProperty("appId").GetString());
        Assert.Equal(requestBody.orderId, payload.GetProperty("orderId").GetString());
    }

    private static WebApplicationFactory<Program> CreateStrictModeFactory()
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<ApiSignOptions>(options => options.StrictMode = true);
                });
            });

    private static async Task<HttpRequestMessage> CreateSignedTransferRequestMessageAsync(
        string appId,
        string nonce,
        long timestamp,
        string secretKey,
        object requestBody,
        bool strictMode,
        IReadOnlyDictionary<string, string>? businessQueryParameters = null,
        IReadOnlyDictionary<string, string>? conflictingQuerySignParameters = null)
    {
        var jsonBody = JsonSerializer.Serialize(requestBody);
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        request.Method = HttpMethods.Post;
        request.ContentType = "application/json";
        request.QueryString = new QueryString(
            BuildQueryString(
                strictMode,
                appId,
                nonce,
                timestamp,
                sign: null,
                businessQueryParameters,
                conflictingQuerySignParameters));

        if (strictMode)
        {
            request.Headers["appId"] = appId;
            request.Headers["nonce"] = nonce;
            request.Headers["timestamp"] = timestamp.ToString();
        }

        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        request.ContentLength = bodyBytes.Length;
        request.Body = new MemoryStream(bodyBytes);

        var extractor = new DefaultSignParameterExtractor(Options.Create(new ApiSignOptions
        {
            StrictMode = strictMode,
        }));
        var parameters = await extractor.ExtractAsync(request);
        var signatureCalculator = new SignatureCalculator();
        var sign = signatureCalculator.Calculate(parameters, secretKey, SignAlgorithm.HMACSHA256);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/payment/transfer")
        {
            Content = JsonContent.Create(requestBody),
        };

        if (strictMode)
        {
            requestMessage.Headers.Add("appId", appId);
            requestMessage.Headers.Add("nonce", nonce);
            requestMessage.Headers.Add("timestamp", timestamp.ToString());
            requestMessage.Headers.Add("sign", sign);
        }

        requestMessage.RequestUri = new Uri(
            $"/api/payment/transfer{BuildQueryString(
                strictMode,
                appId,
                nonce,
                timestamp,
                sign,
                businessQueryParameters,
                conflictingQuerySignParameters)}",
            UriKind.Relative);

        return requestMessage;
    }

    private static string BuildQueryString(
        bool strictMode,
        string appId,
        string nonce,
        long timestamp,
        string? sign,
        IReadOnlyDictionary<string, string>? businessQueryParameters,
        IReadOnlyDictionary<string, string>? conflictingQuerySignParameters)
    {
        var queryParameters = new List<KeyValuePair<string, string>>();

        if (businessQueryParameters is not null)
        {
            queryParameters.AddRange(businessQueryParameters);
        }

        if (strictMode)
        {
            if (conflictingQuerySignParameters is not null)
            {
                queryParameters.AddRange(conflictingQuerySignParameters);
            }
        }
        else
        {
            queryParameters.Add(new("appId", appId));
            queryParameters.Add(new("nonce", nonce));
            queryParameters.Add(new("timestamp", timestamp.ToString()));

            if (!string.IsNullOrWhiteSpace(sign))
            {
                queryParameters.Add(new("sign", sign));
            }
        }

        if (queryParameters.Count == 0)
        {
            return string.Empty;
        }

        return "?" + string.Join(
            "&",
            queryParameters.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }
}