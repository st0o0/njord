using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.Forecast.Tests;

public sealed class DomainPointSpec
{
    private static readonly DateTimeOffset At = new(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);

    public static TheoryData<IHasValidTime> Points => new()
    {
        new WeatherPoint(At, 20, 19, 3, 0.5, 40, 60, 1013, 3, 55, 10, 4),
        new AirQualityPoint(At, 8, 12, 60),
        new FloodPoint(At, 120.5),
        new MarinePoint(At, 1.5, 6, 200),
    };

    [Theory]
    [MemberData(nameof(Points))]
    public void Should_expose_valid_from_for_every_point_type(IHasValidTime point)
    {
        Assert.Equal(At, point.ValidFrom);
    }

    [Fact]
    public void Should_default_optional_weather_measurements_to_null()
    {
        var point = new WeatherPoint(At, 20, 19, 3, 0.5, 40, 60, 1013, 3, 55, 10, 4);

        Assert.Multiple(
            () => Assert.Null(point.EnsembleStdDev),
            () => Assert.Null(point.WindGusts),
            () => Assert.Null(point.IsDay));
    }
}
