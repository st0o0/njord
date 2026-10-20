using Njord.Messages.Pipeline;
using Njord.Scores;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Scores.Tests;

public sealed class BBQScoreHandlerSpec
{
    private static readonly BBQScoreHandler Handler = new();

    [Fact]
    public void Handle_scores_an_ideal_evening_at_least_ninety()
    {
        var forecast = WeatherOnly(Uniform(point => point with { Temperature = 24.0, WindSpeed = 2.0, CloudCover = 20.0 }));

        var score = Assert.IsType<ScoreResult>(Assert.Single(Handler.Handle(forecast, null, Time)));

        Assert.Multiple(
            () => Assert.True(score.Score >= 90, $"score was {score.Score}"),
            () => Assert.Equal(24, score.BestHours));
    }

    [Fact]
    public void Handle_finds_the_dry_window_between_showers()
    {
        var forecast = WeatherOnly(Day((hour, point) => point with
        {
            Temperature = 24.0,
            WindSpeed = 2.0,
            CloudCover = 20.0,
            Precipitation = hour is < 4 or >= 10 ? 2.0 : 0.0,
        }));

        var score = Assert.IsType<ScoreResult>(Assert.Single(Handler.Handle(forecast, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(4), score.BestStart),
            () => Assert.Equal(6, score.BestHours));
    }

    [Fact]
    public void Handle_reports_no_window_when_it_rains_all_day()
    {
        var forecast = WeatherOnly(Uniform(point => point with { Precipitation = 3.0 }));

        var score = Assert.IsType<ScoreResult>(Assert.Single(Handler.Handle(forecast, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(0, score.Score),
            () => Assert.Equal(0, score.BestHours));
    }
}
