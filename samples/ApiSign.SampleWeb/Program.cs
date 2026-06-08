using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Diagnostics;
using ApiSign.AspNetCore.Extensions;
using ApiSign.AspNetCore.Models;
using ApiSign.SampleWeb.Infrastructure;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var redisNonceOptions = builder.Configuration
    .GetSection(RedisNonceOptions.SectionName)
    .Get<RedisNonceOptions>() ?? new RedisNonceOptions();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IAppSecretProvider, SampleAppSecretProvider>();
builder.Services.AddSingleton<IApiSignFailureResponseHandler, SampleApiSignFailureResponseHandler>();
builder.Services.Configure<RedisNonceOptions>(
    builder.Configuration.GetSection(RedisNonceOptions.SectionName));

if (redisNonceOptions.Enabled)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisNonceOptions.Configuration;
        options.InstanceName = redisNonceOptions.InstanceName;
    });
    builder.Services.AddSingleton<INonceStore, RedisNonceStore>();
}

builder.Services.AddApiSignAuthentication(options =>
{
    options.TimestampDisparitySeconds = 900;
    options.EnableNonce = true;
    options.DefaultAlgorithm = SignAlgorithm.HMACSHA256;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(ApiSignDiagnostics.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(ApiSignDiagnostics.MeterName)
        .AddConsoleExporter());

builder.Logging.AddOpenTelemetry(logging => logging.AddConsoleExporter());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.UseApiSignAuthentication(excludedPaths: new[] { "/health", "/api/payment/public-key" });
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;