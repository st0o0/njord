using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results.Trends;

namespace Njord.Grpc.Tests.Mapping;

public sealed class TrendValueMappingTests
{
    private static readonly Location _vienna = new("_vienna", 48.2, 16.37);

    [Fact]
    public void Confidence_trend_maps_direction_and_values()
    {
        var result = new ConfidenceTrendResult(_vienna, ConfidenceTrendDirection.Converging, -0.5, 1.0, 1.5);

        var proto = result.ToTrendValue();

        Assert.Equal((double)ConfidenceTrendDirection.Converging, proto.Extra["direction"]);
        Assert.Equal(-0.5, proto.Extra["magnitude"]);
        Assert.Equal(1.0, proto.Extra["current_avg_std_dev"]);
        Assert.Equal(1.5, proto.Extra["previous_avg_std_dev"]);
    }

    [Fact]
    public void Pollen_trend_maps_available_types()
    {
        var result = new PollenTrendResult(_vienna,
            PeakType: PollenType.Grass,
            PeakTime: new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
            Birch: PollenTrendDirection.Rising,
            Grass: PollenTrendDirection.Stable,
            Alder: null,
            Mugwort: null);

        var proto = result.ToTrendValue();

        Assert.Equal((double)PollenType.Grass, proto.Extra["peak_type"]);
        Assert.Equal((double)PollenTrendDirection.Rising, proto.Extra["birch"]);
        Assert.Equal((double)PollenTrendDirection.Stable, proto.Extra["grass"]);
        Assert.False(proto.Extra.ContainsKey("alder"));
        Assert.False(proto.Extra.ContainsKey("mugwort"));
    }

    [Fact]
    public void Pollen_trend_omits_null_peak()
    {
        var result = new PollenTrendResult(_vienna,
            PeakType: null, PeakTime: null,
            Birch: PollenTrendDirection.Falling, Grass: null, Alder: null, Mugwort: null);

        var proto = result.ToTrendValue();

        Assert.False(proto.Extra.ContainsKey("peak_type"));
        Assert.False(proto.Extra.ContainsKey("peak_time"));
        Assert.Equal((double)PollenTrendDirection.Falling, proto.Extra["birch"]);
    }
}
