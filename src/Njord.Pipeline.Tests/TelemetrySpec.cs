namespace Njord.Pipeline.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Pipeline_instruments_have_expected_names_units_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_pipeline_data_changed_total", Telemetry.DataChanged.Name),
            () => Assert.Equal("{change}", Telemetry.DataChanged.Unit),
            () => Assert.Equal("njord_pipeline_domain_polls_total", Telemetry.DomainPolls.Name),
            () => Assert.Equal("njord_pipeline_cycle_learned_total", Telemetry.CycleLearned.Name),
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
