using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Njord.OpenMeteo.Forecast;
using Njord.OpenMeteo.Http;

namespace Njord.OpenMeteo.Tests;

public sealed class ForecastModelResponseTests
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static ForecastResponse Parse(string json) =>
        Assert.IsType<ForecastResponse>(JsonSerializer.Deserialize(json, OpenMeteoJsonContext.Default.ForecastResponse));

    [Fact]
    public void A_single_model_response_with_plain_keys_is_keyed_by_the_requested_model()
    {
        var forecasts = Parse(OpenMeteoFixtures.ForecastSingleModel).ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        var forecast = Assert.Single(forecasts);
        Assert.Multiple(
            () => Assert.Equal("ecmwf_ifs025_ensemble_mean", forecast.Key),
            () => Assert.Equal(20.5, forecast.Value.Points[0].Temperature));
    }

    [Fact]
    public void A_multi_model_response_splits_suffixed_keys_into_one_forecast_per_model()
    {
        var response = Parse(OpenMeteoFixtures.ForecastThreeModels);

        var forecasts = response.ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.Multiple(
            () => Assert.Equal(2, forecasts.Count),
            () => Assert.Equal(20.5, forecasts["ecmwf_ifs025_ensemble_mean"].Points[0].Temperature),
            () => Assert.Equal(19.5, forecasts["ecmwf_aifs025_ensemble_mean"].Points[0].Temperature));
    }

    [Fact]
    public void Member_series_are_ignored()
    {
        var forecasts = Parse(OpenMeteoFixtures.ForecastThreeModels).ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.DoesNotContain(forecasts.Keys, k => k.StartsWith("member", StringComparison.Ordinal));
    }

    [Fact]
    public void A_model_whose_values_are_all_null_is_omitted()
    {
        var forecasts = Parse(OpenMeteoFixtures.ForecastThreeModels).ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.False(forecasts.ContainsKey("ukmo_global_ensemble_mean_20km"));
    }

    [Fact]
    public void A_response_without_hourly_data_yields_no_forecast()
    {
        var forecasts = Parse("""{"latitude":47.0,"longitude":8.0}""").ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.Empty(forecasts);
    }

    [Fact]
    public void Unknown_keys_are_silently_dropped()
    {
        var forecasts = Parse("""{"latitude":47.0,"longitude":8.0,"hourly":{"time":[1000],"temperature_2m":[20.5],"unknown_variable":[99.0]}}""")
            .ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        var forecast = Assert.Single(forecasts);
        Assert.Equal(20.5, forecast.Value.Points[0].Temperature);
    }

    [Fact]
    public void Every_hourly_property_name_is_in_the_api_variable_names_cache()
    {
        var names = typeof(ForecastHourlyData).GetConstructors()[0].GetParameters()
            .Select(p => typeof(ForecastHourlyData).GetProperty(p.Name!)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .OfType<string>()
            .Where(n => n != "time")
            .ToList();

        var apiNames = ApiVariableNames.For<ForecastApiVariable>().Values.ToHashSet();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.True(apiNames.Contains(n), n));
        Assert.Equal(names.Count, apiNames.Count);
    }
}
