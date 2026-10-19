namespace Njord.Budget.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void The_meter_name_constant_matches_the_budget_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord.Budget", Telemetry.MeterName),
            () => Assert.Equal(Telemetry.MeterName, Telemetry.Meter.Name));
    }
}
