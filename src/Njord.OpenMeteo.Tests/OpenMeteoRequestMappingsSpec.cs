using System.Globalization;
using Njord.Core.Configuration;
using Njord.OpenMeteo.Mapping;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoRequestMappingsSpec
{
    private static readonly LocationOptions Home = new() { Name = "home", Latitude = 47.05, Longitude = 8.31 };

    [Fact]
    public void The_uri_carries_location_variables_and_fixed_query_parameters()
    {
        var uri = Home.ToEnsembleRequestUri(4, "temperature_2m,wind_speed_10m");

        Assert.Equal(
            "v1/ensemble?latitude=47.05&longitude=8.31&hourly=temperature_2m,wind_speed_10m" +
            "&wind_speed_unit=ms&timeformat=unixtime&timezone=UTC&forecast_days=4",
            uri);
    }

    [Fact]
    public void The_forecast_days_are_passed_through()
    {
        var uri = Home.ToEnsembleRequestUri(10, "temperature_2m");

        Assert.EndsWith("&forecast_days=10", uri);
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
