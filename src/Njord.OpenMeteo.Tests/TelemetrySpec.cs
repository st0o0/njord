namespace Njord.OpenMeteo.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Fetch_instruments_have_expected_names_units_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_openmeteo_fetch_total", Telemetry.FetchTotal.Name),
            () => Assert.Equal("{request}", Telemetry.FetchTotal.Unit),
            () => Assert.Equal("Njord.OpenMeteo", Telemetry.FetchTotal.Meter.Name),
            () => Assert.Equal("njord_openmeteo_fetch_duration_seconds", Telemetry.FetchDuration.Name),
            () => Assert.Equal("s", Telemetry.FetchDuration.Unit),
            () => Assert.Equal("Njord.OpenMeteo", Telemetry.FetchDuration.Meter.Name));
    }

    [Fact]
    public void The_meter_name_constant_matches_the_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord.OpenMeteo", Telemetry.MeterName),
            () => Assert.Equal(Telemetry.MeterName, Telemetry.FetchTotal.Meter.Name));
    }
}
