using System.Globalization;
using Njord.Model;

namespace Njord.OpenMeteo.Tests;

public sealed class OpenMeteoUriBuilderSpec
{
    [Fact]
    public void The_air_quality_uri_carries_location_and_fixed_query_parameters()
    {
        var uri = AirQualityClient.BuildUri(47.05, 8.31, 4);

        Assert.Equal(
            "v1/air-quality?latitude=47.05&longitude=8.31" +
            $"&hourly={AirQualityClient.HourlyVariables}&forecast_days=4&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact]
    public void The_flood_uri_carries_location_and_the_fixed_horizon()
    {
        var uri = FloodClient.BuildUri(47.05, 8.31);

        Assert.Equal(
            "v1/flood?latitude=47.05&longitude=8.31" +
            $"&daily={FloodClient.DailyVariables}&forecast_days=92&timeformat=unixtime&timezone=UTC",
            uri);
    }

    [Fact]
    public void Coordinates_use_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var uri = AirQualityClient.BuildUri(47.05, 8.31, 4);

            Assert.Contains("latitude=47.05&longitude=8.31", uri);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
