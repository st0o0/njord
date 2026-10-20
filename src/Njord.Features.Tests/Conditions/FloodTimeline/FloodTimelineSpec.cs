using Microsoft.Extensions.Options;
using Njord.Core.Domain;
using Njord.Features.Conditions.FloodTimeline;
using Njord.Forecast.Flood;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;
using F = Njord.Tests.Shared.FloodFixtures;

namespace Njord.Features.Tests.Conditions;

public sealed class FloodTimelineSpec
{
    private static FloodTimeline Feature() => new(Options.Create(new FloodTimelineConfig { Enabled = true }));

    private static LocationForecast With(params FloodPoint[] points) =>
        WeatherOnly(Day()) with { Flood = F.FloodForecast(points) };

    [Fact]
    public void The_peak_day_and_the_days_above_the_warning_level_are_reported()
    {
        var forecast = With(F.Point(0, 100), F.Point(1, 140), F.Point(2, 180), F.Point(3, 300), F.Point(4, 200), F.Point(5, 120));

        var timeline = Assert.IsType<FloodTimelineResult>(Assert.Single(Run(Feature(), Now, forecast)));

        Assert.Multiple(
            () => Assert.Equal("above_warning", timeline.State),
            () => Assert.Equal(FeatureData.Time(F.Today.AddDays(3)), timeline.Data["peak_time"]),
            () => Assert.Equal("300", timeline.Data["peak_discharge"]),
            () => Assert.Equal("3", timeline.Data["days_above_warning"]));
    }

    [Fact]
    public void Zero_days_are_reported_when_discharge_stays_below_the_warning_level()
    {
        var timeline = Assert.IsType<FloodTimelineResult>(Assert.Single(Run(Feature(), Now, With(F.Point(0, 80), F.Point(1, 90)))));

        Assert.Multiple(
            () => Assert.Equal("normal", timeline.State),
            () => Assert.Equal("0", timeline.Data["days_above_warning"]));
    }

    [Fact]
    public void Nothing_is_reported_without_flood_data()
    {
        Assert.Empty(Run(Feature(), Now, WeatherOnly(Day())));
    }

    [Fact]
    public void The_feature_requires_discharge_and_the_warning_level()
    {
        Assert.Equal(
            new DomainRequirements(Flood: FloodParams.Discharge | FloodParams.DischargeP75),
            Feature().Descriptor.Requirements);
    }
}
