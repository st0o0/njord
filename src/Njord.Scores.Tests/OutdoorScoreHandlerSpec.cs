using Njord.Messages.Pipeline;
using Njord.Scores;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Scores.Tests;

public sealed class OutdoorScoreHandlerSpec
{
    private static readonly OutdoorScoreHandler Handler = new();

    [Fact]
    public void Handle_scores_a_perfect_day_highly_over_the_whole_day()
    {
        var weather = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0 });

        var score = Assert.IsType<ScoreResult>(Assert.Single(Handler.Handle(Combined(weather, CleanAir()), null, Time)));

        Assert.Multiple(
            () => Assert.True(score.Score >= 85, $"score was {score.Score}"),
            () => Assert.Equal(Now, score.BestStart),
            () => Assert.Equal(24, score.BestHours));
    }

    [Fact]
    public void Handle_moves_the_best_window_away_from_polluted_hours()
    {
        var weather = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0 });
        var air = CleanAir(shape: (hour, point) => hour < 6 ? point with { Pm25 = 80.0 } : point);

        var score = Assert.IsType<ScoreResult>(Assert.Single(Handler.Handle(Combined(weather, air), null, Time)));

        Assert.Equal(Now.AddHours(6), score.BestStart);
    }

    [Fact]
    public void Handle_returns_nothing_without_air_quality_data()
    {
        Assert.Empty(Handler.Handle(WeatherOnly(Day()), null, Time));
    }
}
