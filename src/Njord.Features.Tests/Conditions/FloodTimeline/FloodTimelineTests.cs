using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Core.Flood;
using Njord.Features.Conditions.FloodTimeline;
using Njord.Messages.Results.Conditions;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;
using F = Njord.Tests.Shared.FloodFixtures;

namespace Njord.Features.Tests.Conditions.FloodTimeline;

public sealed class FloodTimelineTests
{
    private static Features.Conditions.FloodTimeline.FloodTimeline Feature() => new(Options.Create(new FloodTimelineConfig { Enabled = true }));

    private static LocationForecast With(params FloodPoint[] points) =>
        ForecastOnly(Day()) with { Flood = F.FloodForecast(points) };

    [Fact]
    public void The_peak_day_and_the_days_above_the_warning_level_are_reported()
    {
        var forecast = With(F.Point(0, 100), F.Point(1, 140), F.Point(2, 180), F.Point(3, 300), F.Point(4, 200), F.Point(5, 120));

        var timeline = Assert.IsType<FloodTimelineResult>(Run(Feature(), Now, forecast));

        Assert.Multiple(
            () => Assert.Equal(FloodStatus.AboveWarning, timeline.Status),
            () => Assert.Equal(F.Today.AddDays(3), timeline.PeakTime),
            () => Assert.Equal(300.0, timeline.PeakDischarge),
            () => Assert.Equal(3, timeline.DaysAboveWarning));
    }

    [Fact]
    public void Zero_days_are_reported_when_discharge_stays_below_the_warning_level()
    {
        var timeline = Assert.IsType<FloodTimelineResult>(Run(Feature(), Now, With(F.Point(0, 80), F.Point(1, 90))));

        Assert.Multiple(
            () => Assert.Equal(FloodStatus.Normal, timeline.Status),
            () => Assert.Equal(0, timeline.DaysAboveWarning));
    }

    [Fact]
    public void Nothing_is_reported_without_flood_data()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Day())));
    }

    [Fact]
    public void The_feature_requires_discharge_and_the_warning_level()
    {
        Assert.Equal(
            new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeP75),
            Feature().Descriptor.Requirements);
    }
}
