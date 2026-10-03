using System.Diagnostics.Metrics;

namespace Njord.Diagnostics;

public static class PipelineMetricsExtensions
{
    public static Histogram<double> AddPollCycleDuration(this NjordMetrics m) =>
        m.Meter.CreateHistogram<double>("njord_poll_cycle_duration_seconds", "s");

    public static Gauge<double> AddPollCycleModels(this NjordMetrics m) =>
        m.Meter.CreateGauge<double>("njord_poll_cycle_models", "{model}");

    public static Counter<long> AddDataChanged(this NjordMetrics m) =>
        m.Meter.CreateCounter<long>("njord_data_changed_total", "{change}");
}
