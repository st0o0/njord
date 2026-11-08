using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
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

    [Fact]
    public void ScoreHour_returns_zero_at_night()
    {
        var point = new ForecastPoint(Now, IsDay: 0.0, Temperature: 25.0, Humidity: 40.0, WindSpeed: 5.0, PrecipitationProbability: 0.0, CloudCover: 0.0);
        Assert.Equal(0.0, LaundryScore.ScoreHour(point));
    }

    [Fact]
    public void ScoreHour_returns_zero_with_rain()
    {
        var point = new ForecastPoint(Now, IsDay: 1.0, Temperature: 25.0, Humidity: 40.0, WindSpeed: 5.0, PrecipitationProbability: 0.0, CloudCover: 0.0, Precipitation: 0.5);
        Assert.Equal(0.0, LaundryScore.ScoreHour(point));
    }

    [Fact]
    public void ScoreHour_perfect_conditions_yield_100()
    {
        var point = new ForecastPoint(Now, IsDay: 1.0, Temperature: 25.0, Humidity: 40.0, WindSpeed: 5.0, PrecipitationProbability: 0.0, CloudCover: 0.0, SunshineDuration: 3600.0);
        Assert.Equal(100.0, LaundryScore.ScoreHour(point));
    }

    [Fact]
    public void ScoreHour_cold_reduces_temperature_factor()
    {
        var warm = new ForecastPoint(Now, IsDay: 1.0, Temperature: 25.0, Humidity: 40.0, WindSpeed: 5.0, PrecipitationProbability: 0.0, CloudCover: 0.0);
        var cold = warm with { Temperature = 10.0 };
        Assert.True(LaundryScore.ScoreHour(cold) < LaundryScore.ScoreHour(warm));
    }

    [Fact]
    public void ScoreHour_high_humidity_reduces_score()
    {
        var dry = new ForecastPoint(Now, IsDay: 1.0, Temperature: 25.0, Humidity: 40.0, WindSpeed: 5.0, PrecipitationProbability: 0.0, CloudCover: 0.0);
        var humid = dry with { Humidity = 80.0 };
        Assert.True(LaundryScore.ScoreHour(humid) < LaundryScore.ScoreHour(dry));
    }

    [Fact]
    public void ScoreHour_uses_sunshine_duration_over_cloud_cover()
    {
        var point = new ForecastPoint(Now, IsDay: 1.0, Temperature: 25.0, Humidity: 40.0, WindSpeed: 5.0, PrecipitationProbability: 0.0, CloudCover: 100.0, SunshineDuration: 3600.0);
        Assert.True(LaundryScore.ScoreHour(point) > 90.0);
    }
}
