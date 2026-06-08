using ApiSign.Benchmarks;

using BenchmarkDotNet.Running;

BenchmarkRunner.Run<SignatureCalculatorBenchmarks>();
BenchmarkRunner.Run<BuildCanonicalStringBenchmarks>();