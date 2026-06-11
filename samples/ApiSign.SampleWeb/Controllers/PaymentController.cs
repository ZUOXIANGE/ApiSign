using System.Text.Json;

using ApiSign.AspNetCore.Filters;

using Microsoft.AspNetCore.Mvc;

namespace ApiSign.SampleWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public PaymentController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("transfer")]
    [SaCheckSign]
    public IActionResult Transfer([FromBody] TransferRequest request)
    {
        return Ok(new
        {
            success = true,
            appId = HttpContext.Items["ApiSign:AppId"],
            request.OrderId,
            request.Amount,
        });
    }

    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
        => Ok(new
        {
            appId = "demo-app",
            publicKey = "sample-public-key",
        });

    [HttpGet("status")]
    [SaCheckSign]
    public IActionResult GetStatus()
        => Ok(new
        {
            success = true,
            appId = HttpContext.Items["ApiSign:AppId"],
        });

    [HttpPost("callback-simulation")]
    public async Task<IActionResult> CallbackSimulation(
        [FromQuery] string callbackUrl = "http://localhost:5186/api/payment/transfer")
    {
        var client = _httpClientFactory.CreateClient("callback-client");

        var payload = new { orderId = "ORD-2001", status = "paid", amount = 199.00 };
        var response = await client.PostAsJsonAsync(callbackUrl, payload);

        var responseBody = await response.Content.ReadAsStringAsync();
        return Ok(new
        {
            note = "Signed request sent via ApiSignHttpMessageHandler to dynamically configured callback URL.",
            callbackUrl,
            statusCode = (int)response.StatusCode,
            responseBody = JsonSerializer.Deserialize<object>(responseBody),
        });
    }
}

public sealed record TransferRequest(string OrderId, decimal Amount, string Currency);