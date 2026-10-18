using System.Globalization;
using Njord.Core.Configuration;
using Njord.OpenMeteo.Mapping;
using Njord.Model;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoRequestMappingsSpec
{
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };

    [Fact]
    public void The_uri_carries_location_model_variables_and_fixed_query_parameters()
    {
        var uri = Home.ToRequestUri(new WeatherModel("icon_eu"), 4, "temperature_2m,wind_speed_10m", "sunrise,sunset");

        Assert.Equal(
            "v1/forecast?latitude=47.05&longitude=8.31&models=icon_eu&hourly=temperature_2m,wind_speed_10m" +
            "&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days=4&daily=sunrise,sunset",
            uri);
    }

    [Fact]
    public void The_daily_part_is_omitted_without_daily_variables()
    {
        var uri = Home.ToRequestUri(new WeatherModel("icon_eu"), 4, "temperature_2m", "");

        Assert.DoesNotContain("daily=", uri);
        Assert.EndsWith("&forecast_days=4", uri);
    }

    [Fact]
    public void Forecast_days_are_capped_to_the_model_horizon()
    {
        var uri = Home.ToRequestUri(new WeatherModel("icon_d2"), 10, "temperature_2m", "");

        Assert.Contains($"&forecast_days={ModelCoverageRegistry.EffectiveForecastDays("icon_d2", 10)}", uri);
        Assert.DoesNotContain("forecast_days=10", uri);
    }

    [Fact]
    public void Coordinates_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var uri = Home.ToRequestUri(new WeatherModel("icon_eu"), 4, "temperature_2m", "");

            Assert.Contains("latitude=47.05&longitude=8.31", uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_model_id_is_escaped()
    {
        var uri = Home.ToRequestUri(new WeatherModel("a b&c"), 4, "temperature_2m", "");

        Assert.Contains("&models=a%20b%26c&", uri);
    }
}
