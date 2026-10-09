using System.Diagnostics.Metrics;

namespace Njord.Pipeline;

internal static class Telemetry
{
    internal static readonly Meter Meter = new("Njord.Pipeline");

    internal static readonly Histogram<double> PollCycleDuration =
        Meter.CreateHistogram<double>("njord_poll_cycle_duration_seconds", "s");

    internal static readonly Gauge<double> PollCycleModels =
        Meter.CreateGauge<double>("njord_poll_cycle_models", "{model}");

    internal static readonly Counter<long> DataChanged =
        Meter.CreateCounter<long>("njord_data_changed_total", "{change}");

    internal static readonly Histogram<double> ThrottleWait =
        Meter.CreateHistogram<double>("njord_throttle_wait_seconds", "s");
}
