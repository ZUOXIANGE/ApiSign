using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Models;

using BenchmarkDotNet.Attributes;

namespace ApiSign.Benchmarks;

[MemoryDiagnoser]
public class SignatureCalculatorBenchmarks
{
    private readonly SignatureCalculator _calculator = new();
    private const string CanonicalString = "appId=demo-app&amount=100.00&currency=CNY&nonce=abc123&timestamp=1717920000";
    private const string SecretKey = "test-secret-key-32bytes-long!!";

    [Benchmark]
    public string MD5() => _calculator.Calculate(CanonicalString, SecretKey, SignAlgorithm.MD5);

    [Benchmark]
    public string SHA256() => _calculator.Calculate(CanonicalString, SecretKey, SignAlgorithm.SHA256);

    [Benchmark]
    public string HMACSHA256() => _calculator.Calculate(CanonicalString, SecretKey, SignAlgorithm.HMACSHA256);

    [Benchmark]
    public string HMACSHA512() => _calculator.Calculate(CanonicalString, SecretKey, SignAlgorithm.HMACSHA512);
}