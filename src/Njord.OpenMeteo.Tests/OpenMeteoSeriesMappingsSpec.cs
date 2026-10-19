using Njord.Model;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoSeriesMappingsSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
    private const long Hour0 = 1_783_771_200;

    private static OpenMeteoSeries Series(long[] time, params (string Name, double?[] Values)[] variables) =>
        new(time, variables.ToDictionary(v => v.Name, v => v.Values));

    [Fact]
    public void An_ensemble_response_maps_to_hourly_weather_points_and_trims_trailing_empty_hours()
    {
        var response = new OpenMeteoResponse(
            Series([Hour0, Hour0 + 3600, Hour0 + 7200],
                ("temperature_2m", [20.5, 21.0, null]),
                ("wind_speed_10m", [3.0, 4.0, null]),
                ("weather_code", [3, 61, null]),
                ("wind_gusts_10m", [8.0, null, null])),
            null);

        var forecast = response.ToWeatherForecast(Run);

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
        Assert.Null(new OpenMeteoResponse(null, null).ToWeatherForecast(Run));
    }

    [Fact]
    public void An_air_quality_response_maps_to_hourly_points_with_optional_pollen()
    {
        var response = new OpenMeteoResponse(
            Series([Hour0, Hour0 + 3600],
                ("pm2_5", [8.0, null]),
                ("pm10", [15.0, null]),
                ("ozone", [60.0, null]),
                ("birch_pollen", [12.0, null])),
            null);

        var forecast = response.ToAirQualityForecast(Run);

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
        var response = new OpenMeteoResponse(
            null,
            Series([Hour0, Hour0 + 86_400],
                ("river_discharge", [120.0, null]),
                ("river_discharge_p75", [140.0, null])));

        var forecast = response.ToFloodForecast(Run);

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(DomainKind.Flood, forecast.Domain),
            () => Assert.Equal(TimeResolution.Daily, forecast.Resolution),
            () => Assert.Equal(120.0, point.Discharge),
            () => Assert.Equal(140.0, point.DischargeP75),
            () => Assert.Null(point.DischargeMedian));
    }
}
