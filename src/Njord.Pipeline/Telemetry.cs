using System.Diagnostics.Metrics;

namespace Njord.Pipeline;

internal static class Telemetry
{
    public const string MeterName = "Njord.Pipeline";

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> DataChanged =
        Meter.CreateCounter<long>("njord_pipeline_data_changed_total", "{change}");

    internal static readonly Counter<long> PipelineEvents =
        Meter.CreateCounter<long>("njord_pipeline_events_total", "{event}");

    internal static readonly Histogram<double> EnrichmentDuration =
        Meter.CreateHistogram<double>("njord_pipeline_enrichment_duration_seconds", "s");
}
