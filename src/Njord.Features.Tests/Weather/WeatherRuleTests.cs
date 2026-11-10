using Microsoft.Extensions.Options;
using Njord.Core;
using Njord.Core.Domain;
using Njord.Core.Forecast;
using Njord.Features.Weather;
using Njord.Messages.Results;
using static Njord.Tests.Shared.FeatureRunner;

namespace Njord.Features.Tests.Weather;

public sealed class WeatherRuleTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly DateTimeOffset _now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static WeatherRule Rule() => new(Options.Create(new WeatherConfig()));

    private static ForecastPoint Point(
        int hour,
        double? temperature = 20.0,
        double? weatherCode = 0,
        double? cloudCover = null,
        double? dewPoint = null,
        double? visibility = null,
        double? precipitation = null,
        double? windGusts = null) => new(
        _now.AddHours(hour),
        Temperature: temperature,
        ApparentTemperature: 19.0,
        Humidity: 55.0,
        WindSpeed: 3.0,
        WindDirection: 270.0,
        Pressure: 1015.0,
        WeatherCode: weatherCode,
        CloudCover: cloudCover,
        DewPoint: dewPoint,
        Visibility: visibility,
        Precipitation: precipitation,
        WindGusts: windGusts);

    private static LocationForecast Build(params ForecastPoint[] points) => new(
        _vienna,
        new DomainForecast<ForecastPoint>(DomainKind.Forecast, _now, TimeResolution.Hourly, [.. points]));

    [Fact]
    public void Valid_forecast_yields_current_conditions()
    {
        var result = Assert.IsType<WeatherResult>(Run(Rule(), _now, Build(Point(0), Point(1))));

        Assert.Multiple(
            () => Assert.Equal(20.0, result.Temperature),
            () => Assert.Equal(19.0, result.ApparentTemperature),
            () => Assert.Equal(55.0, result.Humidity),
            () => Assert.Equal(3.0, result.WindSpeed),
            () => Assert.Equal(270.0, result.WindDirection),
            () => Assert.Equal(1015.0, result.Pressure),
            () => Assert.Equal("clear", result.Condition));
    }

    [Fact]
    public void Missing_forecast_domain_yields_null()
    {
        var result = Run(Rule(), _now, new LocationForecast(_vienna));

        Assert.Null(result);
    }

    [Fact]
    public void Current_point_without_temperature_yields_null()
    {
        var result = Run(Rule(), _now, Build(Point(0, temperature: null), Point(1)));

        Assert.Null(result);
    }

    [Fact]
    public void Hourly_forecast_contains_future_points_only()
    {
        var result = Assert.IsType<WeatherResult>(Run(Rule(), _now, Build(Point(-1), Point(0), Point(1), Point(2))));

        Assert.Equal([_now.AddHours(1), _now.AddHours(2)], result.HourlyForecast.Select(p => p.Time));
    }

    [Fact]
    public void Extended_fields_populated_when_present()
    {
        var forecast = Build(
            Point(0, cloudCover: 75, dewPoint: 14.2, visibility: 39520, precipitation: 0.5, windGusts: 8.1),
            Point(1));
        var result = Assert.IsType<WeatherResult>(Run(Rule(), _now, forecast));

        Assert.Multiple(
            () => Assert.Equal(75.0, result.CloudCover),
            () => Assert.Equal(14.2, result.DewPoint),
            () => Assert.Equal(39520.0, result.Visibility),
            () => Assert.Equal(0.5, result.Precipitation),
            () => Assert.Equal(8.1, result.WindGusts));
    }

    [Fact]
    public void Extended_fields_null_when_absent()
    {
        var result = Assert.IsType<WeatherResult>(Run(Rule(), _now, Build(Point(0), Point(1))));

        Assert.Multiple(
            () => Assert.Null(result.CloudCover),
            () => Assert.Null(result.DewPoint),
            () => Assert.Null(result.Visibility),
            () => Assert.Null(result.Precipitation),
            () => Assert.Null(result.WindGusts));
    }

    [Theory]
    [InlineData(0, "clear")]
    [InlineData(3, "overcast")]
    [InlineData(61, "rain")]
    [InlineData(71, "snow")]
    [InlineData(95, "thunderstorm")]
    [InlineData(123, "unknown")]
    public void Weather_code_maps_to_condition(double code, string expected)
    {
        var result = Assert.IsType<WeatherResult>(Run(Rule(), _now, Build(Point(0, weatherCode: code))));

        Assert.Equal(expected, result.Condition);
    }
}
