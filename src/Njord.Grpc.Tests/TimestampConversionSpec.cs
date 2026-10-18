using Google.Protobuf.WellKnownTypes;

namespace Njord.Grpc.Tests;

public sealed class TimestampConversionSpec
{
    [Fact]
    public void A_date_time_offset_converts_to_the_same_instant_as_a_protobuf_timestamp()
    {
        var value = new DateTimeOffset(2026, 7, 15, 13, 0, 0, TimeSpan.FromHours(2));

        var timestamp = value.ToTimestamp();

        Assert.Multiple(
            () => Assert.Equal(value, timestamp.ToDateTimeOffset()),
            () => Assert.Equal(new DateTimeOffset(2026, 7, 15, 11, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), timestamp.Seconds));
    }
}
