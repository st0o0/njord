using System.Text.Json;
using Njord.Core.Forecast;
using Njord.OpenMeteo.Ensemble;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Tests;

public sealed class EnsembleResponseMappingTests
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static ForecastResponse Parse(string json) =>
        Assert.IsType<ForecastResponse>(JsonSerializer.Deserialize(json, OpenMeteoJsonContext.Default.ForecastResponse));

    [Fact]
    public void Extracts_temperature_spread_from_plain_keys()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread":[1.7]}}""");

        var forecast = response.ToEnsembleForecast(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Equal(1.7, point.TemperatureSpread);
    }

    [Fact]
    public void Extracts_spread_from_model_suffixed_keys()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread_ecmwf_ifs025_ensemble_mean":[2.3],"precipitation_spread_ecmwf_ifs025_ensemble_mean":[0.5],"wind_speed_10m_spread_ecmwf_ifs025_ensemble_mean":[1.1]}}""");

        var forecast = response.ToEnsembleForecast(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Multiple(
            () => Assert.Equal(2.3, point.TemperatureSpread),
            () => Assert.Equal(0.5, point.PrecipitationSpread),
            () => Assert.Equal(1.1, point.WindSpeedSpread));
    }

    [Fact]
    public void Returns_null_when_no_hourly_data()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0}""");

        Assert.Null(response.ToEnsembleForecast(Run, "ecmwf_ifs025_ensemble_mean"));
    }

    [Fact]
    public void Returns_null_when_no_time_key()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"temperature_2m_spread":[1.0]}}""");

        Assert.Null(response.ToEnsembleForecast(Run, "ecmwf_ifs025_ensemble_mean"));
    }

    [Fact]
    public void Null_spread_values_are_preserved()
    {
        var response = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m_spread":[null]}}""");

        var forecast = response.ToEnsembleForecast(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.NotNull(forecast);
        var point = Assert.Single(forecast.Points);
        Assert.Null(point.TemperatureSpread);
    }
}
