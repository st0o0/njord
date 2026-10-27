using System.Text.Json;
using Njord.OpenMeteo.AirQuality;
using Njord.OpenMeteo.Flood;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Http;
using Njord.OpenMeteo.Mapping;
using Njord.OpenMeteo.Marine;

namespace Njord.OpenMeteo.Tests.Mapping;

public sealed class NullPassthroughTests
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private const long Hour0 = 1_783_771_200;
    private const string Model = "ecmwf_ifs025_ensemble_mean";

    private static ForecastResponse ParseWeather(string json) =>
        JsonSerializer.Deserialize(json, OpenMeteoJsonContext.Default.ForecastResponse)!;

    [Fact]
    public void Should_leave_weather_fields_null_when_the_response_lacks_the_variable()
    {
        var response = ParseWeather("""
            {"latitude":47.0,"longitude":8.0,"hourly":{"time":[1783771200],"temperature_2m":[0.0]}}
            """);

        var forecasts = response.ToModelForecasts(Run, Model);

        var point = Assert.Single(Assert.Single(forecasts).Value.Points);
        Assert.Multiple(
            () => Assert.Equal(0.0, point.Temperature),
            () => Assert.Null(point.WindSpeed),
            () => Assert.Null(point.WeatherCode),
            () => Assert.Null(point.Humidity),
            () => Assert.Null(point.IsDay));
    }

    [Fact]
    public void Should_keep_a_zero_temperature_weather_point_instead_of_trimming_it()
    {
        var response = ParseWeather("""
            {"latitude":47.0,"longitude":8.0,"hourly":{"time":[1783771200,1783774800],
            "temperature_2m":[0.0,0.0],"wind_speed_10m":[0.0,null]}}
            """);

        var forecasts = response.ToModelForecasts(Run, Model);
        var forecast = Assert.Single(forecasts).Value;

        Assert.Equal(2, forecast.Points.Length);
    }

    [Fact]
    public void Should_leave_air_quality_fields_null_when_the_response_lacks_the_variable()
    {
        var response = new AirQualityResponse(47.0, 8.0,
            new AirQualityHourlyData(Time: [Hour0], Pm25: [8.0]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(8.0, point.Pm25),
            () => Assert.Null(point.Pm10),
            () => Assert.Null(point.Ozone));
    }

    [Fact]
    public void Should_leave_flood_discharge_null_when_only_statistics_are_delivered()
    {
        var response = new FloodResponse(47.0, 8.0,
            new FloodDailyData(Time: [Hour0], DischargeMedian: [3.5]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Null(point.Discharge),
            () => Assert.Equal(3.5, point.DischargeMedian));
    }

    [Fact]
    public void Should_leave_marine_fields_null_when_the_response_lacks_the_variable()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(Time: [Hour0], WaveHeight: [1.2]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(1.2, point.WaveHeight),
            () => Assert.Null(point.WavePeriod),
            () => Assert.Null(point.WaveDirection),
            () => Assert.Null(point.WaterTemperature),
            () => Assert.Null(point.WindWaveHeight),
            () => Assert.Null(point.SwellWaveHeight),
            () => Assert.Null(point.SwellWavePeriod),
            () => Assert.Null(point.SeaLevelHeight));
    }
}
