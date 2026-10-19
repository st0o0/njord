using Njord.Core.Domain;
using Njord.Model;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests;

public sealed class WeatherAlertProjectorSpec
{
    private static readonly WeatherAlertProjector Projector = new();
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    [Fact]
    public void Requires_only_the_weather_domain()
    {
        Assert.Equal([DomainKind.Weather], Projector.RequiredDomains);
    }

    [Fact]
    public void Project_returns_null_without_weather()
    {
        var input = Projector.Project(new LocationForecast(Lucerne), null);

        Assert.Null(input);
    }

    [Fact]
    public void Project_carries_location_and_weather()
    {
        var weather = Day();

        var input = Projector.Project(new LocationForecast(Lucerne, weather), null);

        Assert.NotNull(input);
        Assert.Equal(Lucerne, input.Location);
        Assert.Same(weather, input.Weather);
        Assert.Null(input.Previous);
    }

    [Fact]
    public void Project_passes_the_previous_weather()
    {
        var current = Day();
        var previous = Day((_, p) => p with { Temperature = 3.0 });

        var input = Projector.Project(new LocationForecast(Lucerne, current), new LocationForecast(Lucerne, previous));

        Assert.NotNull(input);
        Assert.Same(previous, input.Previous);
    }

    [Fact]
    public void Project_leaves_previous_empty_when_the_previous_forecast_had_no_weather()
    {
        var input = Projector.Project(new LocationForecast(Lucerne, Day()), new LocationForecast(Lucerne));

        Assert.NotNull(input);
        Assert.Null(input.Previous);
    }
}
