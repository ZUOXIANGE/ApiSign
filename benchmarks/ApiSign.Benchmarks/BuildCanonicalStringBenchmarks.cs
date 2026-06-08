using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Models;

using BenchmarkDotNet.Attributes;

namespace ApiSign.Benchmarks;

[MemoryDiagnoser]
public class BuildCanonicalStringBenchmarks
{
    private readonly SignatureCalculator _calculator = new();

    private readonly SignParameters _smallParams = new()
    {
        AppId = "demo-app",
        Nonce = "abc123",
        Timestamp = 1717920000,
        OtherParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "amount", "100.00" },
            { "currency", "CNY" },
        },
    };

    private readonly SignParameters _largeParams = new()
    {
        AppId = "demo-app",
        Nonce = "abc123",
        Timestamp = 1717920000,
        OtherParams = CreateLargeParams(),
    };

    [Benchmark]
    public string SmallParameterSet() => _calculator.BuildCanonicalString(_smallParams);

    [Benchmark]
    public string LargeParameterSet() => _calculator.BuildCanonicalString(_largeParams);

    private static Dictionary<string, string> CreateLargeParams()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 50; i++)
        {
            dict[$"param_{i:D3}"] = $"value_{i:D3}";
        }
        return dict;
    }
}