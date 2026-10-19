namespace Njord.Core.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void The_meter_name_constant_matches_the_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord.Core", Telemetry.MeterName),
            () => Assert.Equal(Telemetry.MeterName, Telemetry.Meter.Name));
    }
}
