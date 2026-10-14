using System.Diagnostics.Metrics;

namespace Njord.Pipeline;

internal static class Telemetry
{
    internal static readonly Meter Meter = new("Njord.Pipeline");

    internal static readonly Histogram<double> PollCycleDuration =
        Meter.CreateHistogram<double>("njord_poll_cycle_duration_seconds", "s");

    internal static readonly Gauge<double> PollCycleModels =
        Meter.CreateGauge<double>("njord_poll_cycle_models", "{model}");

    internal static readonly Gauge<double> PollCycleFailedModels =
        Meter.CreateGauge<double>("njord_poll_cycle_failed_models", "{model}");

    internal static readonly Counter<long> DataChanged =
        Meter.CreateCounter<long>("njord_pipeline_data_changed_total", "{change}");

    internal static readonly Counter<long> StreamRewires =
        Meter.CreateCounter<long>("njord_pipeline_stream_rewires_total", "{rewire}");
}
