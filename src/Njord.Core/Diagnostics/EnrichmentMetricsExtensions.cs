using System.Diagnostics.Metrics;

namespace Njord.Diagnostics;

public static class EnrichmentMetricsExtensions
{
    public static Histogram<double> AddEnrichmentDuration(this NjordMetrics m) =>
        m.Meter.CreateHistogram<double>("njord_enrichment_duration_seconds", "s");

    public static Gauge<double> AddConsensusModels(this NjordMetrics m) =>
        m.Meter.CreateGauge<double>("njord_consensus_models", "{model}");

    public static Gauge<double> AddConsensusSpread(this NjordMetrics m) =>
        m.Meter.CreateGauge<double>("njord_consensus_spread_celsius", "Cel");

    public static Gauge<double> AddHistoryMae(this NjordMetrics m) =>
        m.Meter.CreateGauge<double>("njord_history_mae_celsius", "Cel");

    public static Gauge<double> AddHistoryModelWeight(this NjordMetrics m) =>
        m.Meter.CreateGauge<double>("njord_history_model_weight", "1");
}
