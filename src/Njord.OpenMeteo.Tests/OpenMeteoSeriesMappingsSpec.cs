using Njord.Model;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoResponseMappingsSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private const long Hour0 = 1_783_771_200;

    [Fact]
    public void A_weather_response_maps_to_hourly_weather_points_and_trims_trailing_empty_hours()
    {
        var response = new WeatherResponse(47.0, 8.0,
            new WeatherHourlyData(
                Time: [Hour0, Hour0 + 3600, Hour0 + 7200],
                Temperature: [20.5, 21.0, null],
                ApparentTemperature: [18.0, 19.0, null],
                WindSpeed: [3.0, 4.0, null],
                Precipitation: [0.0, 0.0, null],
                PrecipitationProbability: [10.0, 20.0, null],
                CloudCover: [50.0, 60.0, null],
                Pressure: [1013.0, 1012.0, null],
                WeatherCode: [3, 61, null],
                Humidity: [65.0, 70.0, null],
                DewPoint: [12.0, 13.0, null],
                UvIndex: [3.0, 2.0, null],
                WindGusts: [8.0, null, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Weather, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(Run, forecast.ModelRun),
            () => Assert.Equal(2, forecast.Points.Length),
            () => Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(Hour0), forecast.Points[0].ValidFrom),
            () => Assert.Equal(20.5, forecast.Points[0].Temperature),
            () => Assert.Equal(61, forecast.Points[1].WeatherCode),
            () => Assert.Equal(8.0, forecast.Points[0].WindGusts),
            () => Assert.Null(forecast.Points[1].WindGusts));
    }

    [Fact]
    public void A_weather_response_without_hourly_data_maps_to_nothing()
    {
        Assert.Null(new WeatherResponse(0, 0, null).ToDomainForecast(Run));
    }

    [Fact]
    public void An_air_quality_response_maps_to_hourly_points_with_optional_pollen()
    {
        var response = new AirQualityResponse(47.0, 8.0,
            new AirQualityHourlyData(
                Time: [Hour0, Hour0 + 3600],
                Pm25: [8.0, null],
                Pm10: [15.0, null],
                Ozone: [60.0, null],
                BirchPollen: [12.0, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.AirQuality, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(8.0, point.Pm25),
            () => Assert.Equal(60.0, point.Ozone),
            () => Assert.Equal(12.0, point.BirchPollen),
            () => Assert.Null(point.GrassPollen));
    }

    [Fact]
    public void A_flood_response_maps_to_daily_points_and_skips_days_without_discharge()
    {
        var response = new FloodResponse(47.0, 8.0,
            new FloodDailyData(
                Time: [Hour0, Hour0 + 86_400],
                Discharge: [120.0, null],
                DischargeP75: [140.0, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, forecast.Domain),
            () => Assert.Equal(TimeResolution.Daily, forecast.Resolution),
            () => Assert.Equal(120.0, point.Discharge),
            () => Assert.Equal(140.0, point.DischargeP75),
            () => Assert.Null(point.DischargeMedian));
    }

    [Fact]
    public void A_marine_response_maps_to_hourly_points_and_trims_trailing_empty_hours()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(
                Time: [Hour0, Hour0 + 3600, Hour0 + 7200],
                WaveHeight: [0.4, 1.2, null],
                WavePeriod: [4.0, 5.0, null],
                WaveDirection: [270.0, 280.0, null],
                SeaSurfaceTemperature: [19.0, null, null]));

        var forecast = response.ToDomainForecast(Run);

        Assert.NotNull(forecast);
        Assert.Equal(2, forecast.Points.Length);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Marine, forecast.Domain),
            () => Assert.Equal(TimeResolution.Hourly, forecast.Resolution),
            () => Assert.Equal(0.4, forecast.Points[0].WaveHeight),
            () => Assert.Equal(4.0, forecast.Points[0].WavePeriod),
            () => Assert.Equal(270.0, forecast.Points[0].WaveDirection),
            () => Assert.Equal(19.0, forecast.Points[0].WaterTemperature),
            () => Assert.Equal(1.2, forecast.Points[1].WaveHeight),
            () => Assert.Null(forecast.Points[1].WaterTemperature));
    }

    [Fact]
    public void A_marine_response_without_any_wave_value_maps_to_nothing()
    {
        var response = new MarineResponse(47.0, 8.0,
            new MarineHourlyData(Time: [Hour0], WaveHeight: [null], WavePeriod: [null], WaveDirection: [null]));

        Assert.Null(response.ToDomainForecast(Run));
    }
}
