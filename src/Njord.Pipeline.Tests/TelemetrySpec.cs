namespace Njord.Pipeline.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Pipeline_instruments_have_expected_names_units_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_poll_cycle_duration_seconds", Telemetry.PollCycleDuration.Name),
            () => Assert.Equal("s", Telemetry.PollCycleDuration.Unit),
            () => Assert.Equal("njord_poll_cycle_models", Telemetry.PollCycleModels.Name),
            () => Assert.Equal("{model}", Telemetry.PollCycleModels.Unit),
            () => Assert.Equal("njord_poll_cycle_failed_models", Telemetry.PollCycleFailedModels.Name),
            () => Assert.Equal("{model}", Telemetry.PollCycleFailedModels.Unit),
            () => Assert.Equal("njord_pipeline_data_changed_total", Telemetry.DataChanged.Name),
            () => Assert.Equal("{change}", Telemetry.DataChanged.Unit),
            () => Assert.Equal("Njord.Pipeline", Telemetry.Meter.Name));
    }
}
