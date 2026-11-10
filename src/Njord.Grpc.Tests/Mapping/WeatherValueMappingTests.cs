using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Results;

namespace Njord.Grpc.Tests.Mapping;

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
        var result = new WeatherResult(
            new Location("Vienna", 48.2, 16.37), 18.5, 17.0, 60, 2.5, 270, 1015, "clear", points,
            CloudCover: 75, DewPoint: 14.2, Visibility: 39520, Precipitation: 0.5, WindGusts: 8.1);

        var value = result.ToWeatherValue();

        Assert.Multiple(
            () => Assert.Equal(18.5, value.Temperature),
            () => Assert.Equal(17.0, value.ApparentTemperature),
            () => Assert.Equal(60, value.Humidity),
            () => Assert.Equal(2.5, value.WindSpeed),
            () => Assert.Equal(270, value.WindDirection),
            () => Assert.Equal(1015, value.Pressure),
            () => Assert.Equal("clear", value.Condition),
            () => Assert.Equal(2, value.HourlyForecast.Count),
            () => Assert.Equal(75, value.CloudCover),
            () => Assert.Equal(14.2, value.DewPoint),
            () => Assert.Equal(39520, value.Visibility),
            () => Assert.Equal(0.5, value.Precipitation),
            () => Assert.Equal(8.1, value.WindGusts));
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

    [Fact]
    public void Null_extended_fields_not_set_on_proto()
    {
        var result = new WeatherResult(
            new Location("Vienna", 48.2, 16.37), 18.5, 17.0, 60, 2.5, 270, 1015, "clear", []);

        var value = result.ToWeatherValue();

        Assert.Multiple(
            () => Assert.False(value.HasCloudCover),
            () => Assert.False(value.HasDewPoint),
            () => Assert.False(value.HasVisibility),
            () => Assert.False(value.HasPrecipitation),
            () => Assert.False(value.HasWindGusts));
    }
}
