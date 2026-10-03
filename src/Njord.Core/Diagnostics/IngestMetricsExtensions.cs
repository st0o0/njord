using System.Diagnostics.Metrics;

namespace Njord.Diagnostics;

public static class IngestMetricsExtensions
{
    public static Counter<long> AddFetchTotal(this NjordMetrics m) =>
        m.Meter.CreateCounter<long>("njord_fetch_total", "{request}");

    public static Histogram<double> AddFetchDuration(this NjordMetrics m) =>
        m.Meter.CreateHistogram<double>("njord_fetch_duration_seconds", "s");
}
