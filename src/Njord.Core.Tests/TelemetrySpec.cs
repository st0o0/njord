namespace Njord.Core.Tests;

public sealed class TelemetrySpec
{
    [Fact]
    public void Stream_consumer_rewire_counter_has_expected_name_unit_and_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("njord_stream_consumer_rewires_total", Telemetry.StreamConsumerRewires.Name),
            () => Assert.Equal("{rewire}", Telemetry.StreamConsumerRewires.Unit),
            () => Assert.Equal("Njord.Core", Telemetry.StreamConsumerRewires.Meter.Name));
    }

    [Fact]
    public void The_meter_name_constant_matches_the_meter()
    {
        Assert.Multiple(
            () => Assert.Equal("Njord.Core", Telemetry.MeterName),
            () => Assert.Equal(Telemetry.MeterName, Telemetry.Meter.Name));
    }
}
