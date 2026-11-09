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

    private static ForecastPoint Point(int hour, double? temperature = 20.0, double? weatherCode = 0) => new(
        _now.AddHours(hour),
        Temperature: temperature,
        ApparentTemperature: 19.0,
        Humidity: 55.0,
        WindSpeed: 3.0,
        WindDirection: 270.0,
        Pressure: 1015.0,
        WeatherCode: weatherCode);

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
