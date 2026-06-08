using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Models;

namespace ApiSign.AspNetCore.Tests;

public sealed class SignatureCalculatorTests
{
    private readonly SignatureCalculator _calculator = new();

    [Fact]
    public void BuildCanonicalString_SortsAndEncodesParameters()
    {
        var parameters = new SignParameters
        {
            AppId = "client-a",
            Nonce = "nonce 1",
            Timestamp = 1710000000,
            OtherParams = new Dictionary<string, string>
            {
                ["amount"] = "100.50",
                ["subject"] = "test value",
            },
        };

        var canonical = _calculator.BuildCanonicalString(parameters);

        Assert.Equal("amount=100.50&appId=client-a&nonce=nonce%201&subject=test%20value&timestamp=1710000000", canonical);
    }

    [Fact]
    public void Calculate_HmacSha256_ReturnsExpectedHash()
    {
        var parameters = new SignParameters
        {
            AppId = "demo-app",
            Nonce = "abc123",
            Timestamp = 1710000000,
            OtherParams = new Dictionary<string, string>
            {
                ["amount"] = "88",
                ["currency"] = "CNY",
            },
        };

        var signature = _calculator.Calculate(parameters, "secret-001", SignAlgorithm.HMACSHA256);

        Assert.Equal("B998658C6304C687FAD4E92F33FB12D5205ED2182197A7112CFF3D688D20FFE1", signature);
    }
}