using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiSign.AspNetCore.Diagnostics;

public static class ApiSignDiagnostics
{
    public const string ActivitySourceName = "ApiSign.AspNetCore";
    public const string MeterName = "ApiSign.AspNetCore";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Histogram<double> ValidationDuration = Meter.CreateHistogram<double>(
        "apisign.validation.duration",
        "ms",
        "Duration of API signature validation");

    public static readonly Counter<long> ValidationRequests = Meter.CreateCounter<long>(
        "apisign.validation.requests",
        description: "Number of API signature validation requests");
}