using System.Diagnostics.Metrics;

namespace Njord.OpenMeteo;

internal static class Telemetry
{
    internal static readonly Meter Meter = new("Njord.OpenMeteo");

    internal static readonly Counter<long> FetchTotal =
        Meter.CreateCounter<long>("njord_fetch_total", "{request}");

    internal static readonly Histogram<double> FetchDuration =
        Meter.CreateHistogram<double>("njord_fetch_duration_seconds", "s");

    internal static readonly Histogram<double> ThrottleWait =
        Meter.CreateHistogram<double>("njord_gateway_throttle_wait_seconds", "s");
}
