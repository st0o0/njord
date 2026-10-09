using System.Diagnostics.Metrics;

namespace Njord.Ingest;

internal static class Telemetry
{
    private static readonly Meter Meter = new("Njord.Ingest");

    internal static readonly Counter<long> FetchTotal =
        Meter.CreateCounter<long>("njord_fetch_total", "{request}");

    internal static readonly Histogram<double> FetchDuration =
        Meter.CreateHistogram<double>("njord_fetch_duration_seconds", "s");
}
