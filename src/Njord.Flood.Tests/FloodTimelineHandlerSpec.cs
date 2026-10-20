using Njord.Core.Domain;
using Njord.Messages.Pipeline;
using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodTimelineHandlerSpec
{
    private static readonly FloodTimelineHandler Handler = new();

    [Fact]
    public void Handle_reports_the_peak_day_and_the_days_above_the_warning_level()
    {
        var forecast = new LocationForecast(Lucerne, Flood: FloodForecast(
            Point(0, 100), Point(1, 140), Point(2, 180), Point(3, 300), Point(4, 200), Point(5, 120)));

        var timeline = Assert.IsType<PipelineEvent.FloodTimelineComputed>(Assert.Single(Handler.Handle(forecast, null, TimeProvider.System)));

        Assert.Multiple(
            () => Assert.Equal(Today.AddDays(3), timeline.PeakTime),
            () => Assert.Equal(300.0, timeline.PeakDischarge),
            () => Assert.Equal(3, timeline.DaysAboveWarning));
    }

    [Fact]
    public void Handle_reports_zero_days_when_discharge_stays_below_the_warning_level()
    {
        var forecast = new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 80), Point(1, 90)));

        var timeline = Assert.IsType<PipelineEvent.FloodTimelineComputed>(Assert.Single(Handler.Handle(forecast, null, TimeProvider.System)));

        Assert.Equal(0, timeline.DaysAboveWarning);
    }

    [Fact]
    public void Handle_returns_nothing_without_flood_data()
    {
        Assert.Empty(Handler.Handle(new LocationForecast(Lucerne), null, TimeProvider.System));
    }
}
