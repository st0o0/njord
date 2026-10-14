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
}
