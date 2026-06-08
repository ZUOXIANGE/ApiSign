using ApiSign.AspNetCore.Filters;

using Microsoft.AspNetCore.Mvc;

namespace ApiSign.SampleWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentController : ControllerBase
{
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
}

public sealed record TransferRequest(string OrderId, decimal Amount, string Currency);