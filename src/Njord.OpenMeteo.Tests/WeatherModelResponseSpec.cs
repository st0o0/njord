using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class WeatherModelResponseSpec
{
    private static readonly DateTimeOffset Run = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private static WeatherResponse Parse(string json) =>
        Assert.IsType<WeatherResponse>(JsonSerializer.Deserialize(json, OpenMeteoJsonContext.Default.WeatherResponse));

    [Fact]
    public void A_single_model_response_with_plain_keys_is_keyed_by_the_requested_model()
    {
        var forecasts = Parse(OpenMeteoFixtures.WeatherWithSpread).ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        var forecast = Assert.Single(forecasts);
        Assert.Multiple(
            () => Assert.Equal("ecmwf_ifs025_ensemble_mean", forecast.Key),
            () => Assert.Equal(20.5, forecast.Value.Points[0].Temperature),
            () => Assert.Equal(1.7, forecast.Value.Points[0].EnsembleStdDev));
    }

    [Fact]
    public void A_multi_model_response_splits_suffixed_keys_into_one_forecast_per_model()
    {
        var response = Parse(OpenMeteoFixtures.WeatherThreeModels);

        var forecasts = response.ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.Multiple(
            () => Assert.Equal(2, forecasts.Count),
            () => Assert.Equal(20.5, forecasts["ecmwf_ifs025_ensemble_mean"].Points[0].Temperature),
            () => Assert.Equal(1.5, forecasts["ecmwf_ifs025_ensemble_mean"].Points[0].EnsembleStdDev),
            () => Assert.Equal(19.5, forecasts["ecmwf_aifs025_ensemble_mean"].Points[0].Temperature));
    }

    [Fact]
    public void Member_series_are_ignored()
    {
        var response = Parse(OpenMeteoFixtures.WeatherThreeModels);

        Assert.DoesNotContain(response.Models.Keys, k => k.StartsWith("member", StringComparison.Ordinal));
    }

    [Fact]
    public void A_model_whose_values_are_all_null_is_omitted()
    {
        var forecasts = Parse(OpenMeteoFixtures.WeatherThreeModels).ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.False(forecasts.ContainsKey("ukmo_global_ensemble_mean_20km"));
    }

    [Fact]
    public void A_response_without_hourly_data_yields_no_forecast()
    {
        var forecasts = Parse("""{"latitude":47.0,"longitude":8.0}""").ToModelForecasts(Run, "ecmwf_ifs025_ensemble_mean");

        Assert.Empty(forecasts);
    }

    [Fact]
    public void Every_hourly_property_name_is_listed_in_the_converter_names()
    {
        var names = typeof(WeatherHourlyData).GetConstructors()[0].GetParameters()
            .Select(p => typeof(WeatherHourlyData).GetProperty(p.Name!)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .OfType<string>()
            .Where(n => n != "time")
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.True(WeatherResponseConverter.ApiNames.Contains(n), n));
        Assert.Equal(names.Count, WeatherResponseConverter.ApiNames.Count);
    }
}
