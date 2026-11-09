using System.Text.Json;
using Njord.OpenMeteo.Ensemble;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Tests;

public sealed class EnsembleResponseMappingTests
{
    private static readonly DateTimeOffset _run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static ForecastResponse Parse(string json) =>
        Assert.IsType<ForecastResponse>(JsonSerializer.Deserialize(json, OpenMeteoJsonContext.Default.ForecastResponse));

    [Fact]
    public void Extracts_temperature_spread_from_plain_keys()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread":[1.7]}}""");

        var forecast = response.ToEnsembleForecast(_run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Equal(1.7, point.TemperatureSpread);
    }

    [Fact]
    public void Extracts_spread_from_model_suffixed_keys()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread_ecmwf_ifs025_ensemble_mean":[2.3],"precipitation_spread_ecmwf_ifs025_ensemble_mean":[0.5],"wind_speed_10m_spread_ecmwf_ifs025_ensemble_mean":[1.1]}}""");

        var forecast = response.ToEnsembleForecast(_run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(2.3, point.TemperatureSpread),
            () => Assert.Equal(0.5, point.PrecipitationSpread),
            () => Assert.Equal(1.1, point.WindSpeedSpread));
    }

    [Fact]
    public void Extracts_the_expanded_spread_variables()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"pressure_msl_spread":[1.0],"cloud_cover_spread":[2.0],"wind_gusts_10m_spread":[3.0],"cape_spread":[4.0],"snowfall_spread":[5.0],"rain_spread":[6.0],"dew_point_2m_spread":[7.0],"relative_humidity_2m_spread":[8.0],"surface_pressure_spread":[9.0],"wind_direction_10m_spread":[10.0],"wet_bulb_temperature_2m_spread":[11.0],"wind_speed_100m_spread":[12.0]}}""");

        var forecast = response.ToEnsembleForecast(_run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(1.0, point.PressureMslSpread),
            () => Assert.Equal(2.0, point.CloudCoverSpread),
            () => Assert.Equal(3.0, point.WindGustsSpread),
            () => Assert.Equal(4.0, point.CapeSpread),
            () => Assert.Equal(5.0, point.SnowfallSpread),
            () => Assert.Equal(6.0, point.RainSpread),
            () => Assert.Equal(7.0, point.DewPointSpread),
            () => Assert.Equal(8.0, point.HumiditySpread),
            () => Assert.Equal(9.0, point.SurfacePressureSpread),
            () => Assert.Equal(10.0, point.WindDirectionSpread),
            () => Assert.Equal(11.0, point.WetBulbTemperatureSpread),
            () => Assert.Equal(12.0, point.WindSpeed100MSpread));
    }

    [Fact]
    public void Returns_null_when_no_hourly_data()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0}""");

        Assert.Null(response.ToEnsembleForecast(_run, "ecmwf_ifs025_ensemble_mean"));
    }

    [Fact]
    public void Returns_null_when_no_time_key()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"temperature_2m_spread":[1.0]}}""");

        Assert.Null(response.ToEnsembleForecast(_run, "ecmwf_ifs025_ensemble_mean"));
    }

    [Fact]
    public void Null_spread_values_are_preserved()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread":[null]}}""");

        var forecast = response.ToEnsembleForecast(_run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Null(point.TemperatureSpread);
    }
}
