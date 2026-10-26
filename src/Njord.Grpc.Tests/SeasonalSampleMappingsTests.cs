using Njord.Forecast.Seasonal;
using Njord.Grpc.Mapping;

namespace Njord.Grpc.Tests;

public sealed class SeasonalSampleMappingsTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_component_maps_to_its_own_field()
    {
        var sample = new SeasonalPoint(At, 1.5, -12.0, 80.0).ToProto();

        Assert.Multiple(
            () => Assert.Equal(1.5, sample.TemperatureAnomaly),
            () => Assert.Equal(-12.0, sample.PrecipitationAnomaly),
            () => Assert.Equal(80.0, sample.PrecipitationMean),
            () => Assert.Equal(At.ToUnixTimeSeconds(), sample.ValidAt.Seconds));
    }

    [Fact]
    public void Missing_components_stay_unset()
    {
        var sample = new SeasonalPoint(At, TemperatureAnomaly: 0.5).ToProto();

        Assert.Multiple(
            () => Assert.True(sample.HasTemperatureAnomaly),
            () => Assert.False(sample.HasPrecipitationAnomaly),
            () => Assert.False(sample.HasPrecipitationMean));
    }
}
