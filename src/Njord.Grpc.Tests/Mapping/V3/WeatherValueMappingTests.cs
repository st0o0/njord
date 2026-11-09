using Njord.Core;
using Njord.Grpc.Mapping.V3;
using Njord.Messages.Results;

namespace Njord.Grpc.Tests.Mapping.V3;

public sealed class WeatherValueMappingTests
{
    private static readonly DateTimeOffset _time = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Current_fields_and_forecast_points_map()
    {
        var points = new List<WeatherForecastPoint>
        {
            new(_time, 20, 19, 55, 3, 180, 1012, "cloudy", 0.4, 1.2, 80),
            new(_time.AddHours(1), 21, 20, 50, 4, 190, 1011, "sunny"),
        };
        var result = new WeatherResult(new Location("Vienna", 48.2, 16.37), 18.5, 17.0, 60, 2.5, 270, 1015, "clear", points);

        var value = result.ToWeatherValue();

        Assert.Multiple(
            () => Assert.Equal(18.5, value.Temperature),
            () => Assert.Equal(17.0, value.ApparentTemperature),
            () => Assert.Equal(60, value.Humidity),
            () => Assert.Equal(2.5, value.WindSpeed),
            () => Assert.Equal(270, value.WindDirection),
            () => Assert.Equal(1015, value.Pressure),
            () => Assert.Equal("clear", value.Condition),
            () => Assert.Equal(2, value.HourlyForecast.Count));
        var first = value.HourlyForecast[0];
        var second = value.HourlyForecast[1];
        Assert.Multiple(
            () => Assert.Equal(_time, first.Time.ToDateTimeOffset()),
            () => Assert.Equal(20, first.Temperature),
            () => Assert.Equal(19, first.ApparentTemperature),
            () => Assert.Equal(55, first.Humidity),
            () => Assert.Equal(3, first.WindSpeed),
            () => Assert.Equal(180, first.WindDirection),
            () => Assert.Equal(1012, first.Pressure),
            () => Assert.Equal("cloudy", first.Condition),
            () => Assert.Equal(0.4, first.PrecipitationProbability),
            () => Assert.Equal(1.2, first.Precipitation),
            () => Assert.Equal(80, first.CloudCover),
            () => Assert.False(second.HasPrecipitationProbability),
            () => Assert.False(second.HasPrecipitation),
            () => Assert.False(second.HasCloudCover));
    }
}
