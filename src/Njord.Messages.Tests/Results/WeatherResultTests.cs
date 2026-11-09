using Njord.Core;
using Njord.Messages.Results;

namespace Njord.Messages.Tests.Results;

public sealed class WeatherResultTests
{
    private static readonly Location _vienna = new("_vienna", 48.2, 16.37);

    [Fact]
    public void Construction_with_current_conditions()
    {
        var result = new WeatherResult(
            _vienna, 18.5, 16.2, 65, 12.3, 225, 1013.25, "cloudy", []);

        Assert.Equal(_vienna, result.Location);
        Assert.Equal(18.5, result.Temperature);
        Assert.Equal(16.2, result.ApparentTemperature);
        Assert.Equal(65, result.Humidity);
        Assert.Equal(12.3, result.WindSpeed);
        Assert.Equal(225, result.WindDirection);
        Assert.Equal(1013.25, result.Pressure);
        Assert.Equal("cloudy", result.Condition);
        Assert.Empty(result.HourlyForecast);
    }

    [Fact]
    public void Construction_with_hourly_forecast()
    {
        var time = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        var points = new[]
        {
            new WeatherForecastPoint(time, 18.5, 16.2, 65, 12.3, 225, 1013.25, "cloudy"),
            new WeatherForecastPoint(time.AddHours(1), 19.0, 17.0, 60, 11.0, 220, 1013.0, "partly_cloudy"),
        };

        var result = new WeatherResult(
            _vienna, 18.5, 16.2, 65, 12.3, 225, 1013.25, "cloudy", points);

        Assert.Equal(2, result.HourlyForecast.Count);
        Assert.Equal(time, result.HourlyForecast[0].Time);
        Assert.Equal(time.AddHours(1), result.HourlyForecast[1].Time);
    }

    [Fact]
    public void Forecast_point_optional_fields_default_to_null()
    {
        var time = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        var point = new WeatherForecastPoint(time, 18.5, 16.2, 65, 12.3, 225, 1013.25, "clear");

        Assert.Null(point.PrecipitationProbability);
        Assert.Null(point.Precipitation);
        Assert.Null(point.CloudCover);
    }

    [Fact]
    public void Is_a_feature_result()
    {
        var result = new WeatherResult(_vienna, 18.5, 16.2, 65, 12.3, 225, 1013.25, "clear", []);

        Assert.IsAssignableFrom<FeatureResult>(result);
    }
}
