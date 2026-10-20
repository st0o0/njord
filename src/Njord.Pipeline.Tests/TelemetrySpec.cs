namespace Njord.Pipeline.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Pipeline_instruments_have_expected_names_units_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_pipeline_events_total", Telemetry.PipelineEvents.Name),
            () => Assert.Equal("{event}", Telemetry.PipelineEvents.Unit),
            () => Assert.Equal("njord_pipeline_handler_skipped_total", Telemetry.HandlerSkipped.Name),
            () => Assert.Equal("njord_pipeline_producer_failed_total", Telemetry.ProducerFailed.Name),
            () => Assert.Equal("njord_pipeline_enrichment_duration_seconds", Telemetry.EnrichmentDuration.Name),
            () => Assert.Equal("Njord.Pipeline", Telemetry.Meter.Name));
    }

    [Fact]
    public void The_meter_name_constant_matches_the_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord.Pipeline", Telemetry.MeterName),
            () => Assert.Equal(Telemetry.MeterName, Telemetry.Meter.Name));
    }
}
