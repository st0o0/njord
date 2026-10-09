namespace Njord.Enrichment.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Enrichment_instruments_have_expected_names_units_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_enrichment_duration_seconds", Telemetry.EnrichmentDuration.Name),
            () => Assert.Equal("s", Telemetry.EnrichmentDuration.Unit),
            () => Assert.Equal("njord_consensus_models", Telemetry.ConsensusModels.Name),
            () => Assert.Equal("njord_consensus_spread_celsius", Telemetry.ConsensusSpread.Name),
            () => Assert.Equal("njord_history_mae_celsius", Telemetry.HistoryMae.Name),
            () => Assert.Equal("njord_history_model_weight", Telemetry.HistoryModelWeight.Name),
            () => Assert.Equal("Njord.Enrichment", Telemetry.HistoryMae.Meter.Name));
    }
}
