using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.FloodFixtures;

namespace Njord.Advisories.Tests;

public sealed class FloodTimelineHandlerSpec
{
    private static readonly FloodTimelineHandler Handler = new();

    [Fact]
    public void Handle_reports_the_peak_day_and_the_days_above_the_warning_level()
    {
        var forecast = new LocationForecast(Lucerne, Flood: FloodForecast(
            Point(0, 100), Point(1, 140), Point(2, 180), Point(3, 300), Point(4, 200), Point(5, 120)));

        var timeline = Assert.IsType<ConditionResult>(Assert.Single(Handler.Handle(forecast, null, TimeProvider.System)));

        Assert.Multiple(
            () => Assert.Equal("floodtimeline", timeline.WireKey),
            () => Assert.Equal("above_warning", timeline.State),
            () => Assert.Equal(FeatureData.Time(Today.AddDays(3)), timeline.Data["peak_time"]),
            () => Assert.Equal("300", timeline.Data["peak_discharge"]),
            () => Assert.Equal("3", timeline.Data["days_above_warning"]));
    }

    [Fact]
    public void Handle_reports_zero_days_when_discharge_stays_below_the_warning_level()
    {
        var forecast = new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 80), Point(1, 90)));

        var timeline = Assert.IsType<ConditionResult>(Assert.Single(Handler.Handle(forecast, null, TimeProvider.System)));

        Assert.Multiple(
            () => Assert.Equal("normal", timeline.State),
            () => Assert.Equal("0", timeline.Data["days_above_warning"]));
    }

    [Fact]
    public void Handle_returns_nothing_without_flood_data()
    {
        Assert.Empty(Handler.Handle(new LocationForecast(Lucerne), null, TimeProvider.System));
    }
}
