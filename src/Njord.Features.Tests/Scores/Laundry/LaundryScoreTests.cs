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

    [Fact]
    public void Night_hours_score_zero()
    {
        var weather = Uniform(p => p with
        {
            IsDay = 0.0,
            Temperature = 25.0,
            Humidity = 40.0,
            WindSpeed = 5.0,
            CloudCover = 0.0,
        });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Equal(0, score.Score);
    }

    [Fact]
    public void High_humidity_reduces_score()
    {
        var weather = Uniform(p => p with
        {
            Temperature = 25.0,
            Humidity = 85.0,
            WindSpeed = 5.0,
            CloudCover = 0.0,
        });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.True(score.Score < 80, $"Expected reduced score with high humidity but got {score.Score}");
    }

    [Fact]
    public void Cold_temperature_reduces_score()
    {
        var weather = Uniform(p => p with
        {
            Temperature = 5.0,
            Humidity = 40.0,
            WindSpeed = 5.0,
            CloudCover = 0.0,
        });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.True(score.Score < 100, $"Expected reduced score with cold temperature but got {score.Score}");
    }

    [Fact]
    public void Sunshine_duration_boosts_score_over_cloud_cover()
    {
        var weather = Uniform(p => p with
        {
            Temperature = 25.0,
            Humidity = 40.0,
            WindSpeed = 5.0,
            CloudCover = 80.0,
            SunshineDuration = 3600.0,
        });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.True(score.Score >= 90, $"Expected high score with sunshine duration but got {score.Score}");
    }

    [Fact]
    public void Windows_are_reported()
    {
        var weather = Day((hour, p) => (hour < 6 ? p with { Precipitation = 2.0 } : p) with
        {
            Temperature = 25.0,
            Humidity = 40.0,
            WindSpeed = 5.0,
            CloudCover = 0.0,
        });

        var score = Assert.IsType<LaundryScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.NotEmpty(score.Windows);
    }
}
