using System.Diagnostics.Metrics;

namespace Njord.Enrichment;

internal static class Telemetry
{
    private static readonly Meter Meter = new("Njord.Enrichment");

    internal static readonly Histogram<double> EnrichmentDuration =
        Meter.CreateHistogram<double>("njord_enrichment_duration_seconds", "s");

    internal static readonly Gauge<double> ConsensusModels =
        Meter.CreateGauge<double>("njord_consensus_models", "{model}");

    internal static readonly Gauge<double> ConsensusSpread =
        Meter.CreateGauge<double>("njord_consensus_spread_celsius", "Cel");

    internal static readonly Gauge<double> HistoryMae =
        Meter.CreateGauge<double>("njord_history_mae_celsius", "Cel");

    internal static readonly Gauge<double> HistoryModelWeight =
        Meter.CreateGauge<double>("njord_history_model_weight", "1");
}
