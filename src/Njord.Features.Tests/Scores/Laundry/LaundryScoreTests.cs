using Microsoft.Extensions.Options;
using Njord.Features.Scores.Laundry;
using Njord.Messages.Results.Scores;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores.Laundry;

public sealed class LaundryScoreTests
{
    private static LaundryScore Feature() => new(Options.Create(new LaundryConfig { Enabled = true }));

    [Fact]
    public void A_warm_dry_breezy_sunny_day_scores_100_for_all_day()
    {
        var weather = Uniform(p => p with { Temperature = 25.0, Humidity = 40.0, WindSpeed = 5.0, CloudCover = 0.0 });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(100, score.Score),
            () => Assert.Equal(Now, score.BestStart),
            () => Assert.Equal(24, score.BestHours));
    }

    [Fact]
    public void The_dry_stretch_after_rainy_hours_is_found()
    {
        var weather = Day((hour, p) => (hour < 6 ? p with { Precipitation = 2.0 } : p) with
        {
            Temperature = 25.0,
            Humidity = 40.0,
            WindSpeed = 5.0,
            CloudCover = 0.0,
        });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(6), score.BestStart),
            () => Assert.Equal(18, score.BestHours));
    }

    [Fact]
    public void Rain_all_day_reports_zero_good_hours()
    {
        var weather = Uniform(p => p with { Precipitation = 1.0 });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(0, score.Score),
            () => Assert.Equal(0, score.BestHours));
    }
}
