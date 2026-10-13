using Njord.Core.Configuration;
using Njord.Weather;

namespace Njord.Core.Tests.Configuration;

public sealed class NjordOptionsSpec
{
    [Fact]
    public void Horizons_default_to_the_six_step_ladder()
    {
        Assert.Equal([3, 6, 12, 24, 48, 72], new NjordOptions().Horizons);
    }

    [Fact]
    public void Holds_only_the_forecast_scope()
    {
        var properties = typeof(NjordOptions).GetProperties().Select(p => p.Name).Order();

        Assert.Equal(["ForecastDays", "Horizons", "Locations", "Models", "Parameters"], properties);
    }

    [Fact]
    public void FindLocation_matches_the_name_case_insensitively()
    {
        var lucerne = new LocationOptions { Name = "Lucerne" };
        var options = new NjordOptions { Locations = [new LocationOptions { Name = "Zurich" }, lucerne] };

        Assert.Same(lucerne, options.FindLocation("lUcErNe"));
    }

    [Fact]
    public void FindLocation_returns_null_when_no_location_matches()
    {
        var options = new NjordOptions { Locations = [new LocationOptions { Name = "Zurich" }] };

        Assert.Null(options.FindLocation("Bern"));
    }
}
