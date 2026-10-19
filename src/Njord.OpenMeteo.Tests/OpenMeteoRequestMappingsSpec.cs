using System.Globalization;
using Njord.Core.Configuration;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoRequestMappingsSpec
{
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };

    [Fact]
    public void The_ensemble_uri_carries_location_variables_and_fixed_query_parameters()
    {
        var uri = Home.ToEnsembleRequestUri(4, "temperature_2m,wind_speed_10m");

        Assert.Equal(
            "v1/ensemble?latitude=47.05&longitude=8.31&hourly=temperature_2m,wind_speed_10m" +
            "&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days=4",
            uri);
    }

    [Fact]
    public void The_air_quality_uri_targets_the_air_quality_host_with_the_forecast_days()
    {
        var uri = OpenMeteoRequestMappings.ToAirQualityRequestUri(47.05, 8.31, 4);

        Assert.Equal(
            "https://air-quality-api.open-meteo.com/v1/air-quality?latitude=47.05&longitude=8.31" +
            $"&hourly={OpenMeteoRequestMappings.AirQualityHourlyVariables}&forecast_days=4&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact]
    public void The_flood_uri_targets_the_flood_host_with_the_fixed_horizon()
    {
        var uri = OpenMeteoRequestMappings.ToFloodRequestUri(47.05, 8.31);

        Assert.Equal(
            "https://flood-api.open-meteo.com/v1/flood?latitude=47.05&longitude=8.31" +
            $"&daily={OpenMeteoRequestMappings.FloodDailyVariables}&forecast_days=92&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact]
    public void Coordinates_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var uri = Home.ToEnsembleRequestUri(4, "temperature_2m");

            Assert.Contains("latitude=47.05&longitude=8.31", uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
