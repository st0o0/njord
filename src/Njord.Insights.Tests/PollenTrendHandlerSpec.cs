using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AirQualityFixture;

namespace Njord.Insights.Tests;

public sealed class PollenTrendHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);
    private static readonly PollenTrendHandler Handler = new();

    private static LocationForecast With(Func<int, AirQualityPoint, AirQualityPoint> shape) =>
        new(Lucerne, AirQuality: Day(shape));

    [Fact]
    public void Handle_classifies_each_pollen_type_against_the_previous_cycle()
    {
        var currentForecast = With((_, p) => p with { BirchPollen = 30.0, GrassPollen = 10.0, Alder = 10.0 });
        var previousForecast = With((_, p) => p with { BirchPollen = 10.0, GrassPollen = 10.0, Alder = 30.0 });

        var events = Handler.Handle(currentForecast, previousForecast, Time);

        var trend = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("rising", trend.Data["trend_birch"]),
            () => Assert.Equal("stable", trend.Data["trend_grass"]),
            () => Assert.Equal("falling", trend.Data["trend_alder"]));
    }

    [Fact]
    public void Handle_reports_the_peak_type_and_time()
    {
        var currentForecast = With((hour, p) => p with { BirchPollen = 5.0, GrassPollen = hour == 7 ? 60.0 : 5.0 });
        var previousForecast = With((_, p) => p with { BirchPollen = 5.0, GrassPollen = 5.0 });

        var events = Handler.Handle(currentForecast, previousForecast, Time);

        var trend = Assert.IsType<ConditionResult>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal("pollentrend", trend.WireKey),
            () => Assert.Equal("grass", trend.State),
            () => Assert.Equal(FeatureData.Time(Now.AddHours(7)), trend.Data["peak_time"]));
    }

    [Fact]
    public void Handle_returns_nothing_without_a_previous_cycle()
    {
        Assert.Empty(Handler.Handle(With((_, p) => p with { BirchPollen = 30.0 }), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_when_no_pollen_is_reported()
    {
        Assert.Empty(Handler.Handle(With((_, p) => p), With((_, p) => p), Time));
    }
}
