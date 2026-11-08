using Microsoft.Extensions.Options;
using Njord.Features.Scores.Outdoor;
using Njord.Messages.Results.Scores;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores.Outdoor;

public sealed class OutdoorScoreTests
{
    private static OutdoorScore Feature() => new(Options.Create(new OutdoorConfig { Enabled = true }));

    [Fact]
    public void A_perfect_day_scores_highly_over_the_whole_day()
    {
        var weather = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0 });

        var score = Assert.IsType<OutdoorScoreResult>(Run(Feature(), Now, Combined(weather, CleanAir())));

        Assert.Multiple(
            () => Assert.True(score.Score >= 85, $"score was {score.Score}"),
            () => Assert.Equal(Now, score.BestStart),
            () => Assert.Equal(24, score.BestHours));
    }

    [Fact]
    public void The_best_window_moves_away_from_polluted_hours()
    {
        var weather = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0 });
        var air = CleanAir(shape: (hour, point) => hour < 6 ? point with { Pm25 = 80.0 } : point);

        var score = Assert.IsType<OutdoorScoreResult>(Run(Feature(), Now, Combined(weather, air)));

        Assert.Equal(Now.AddHours(6), score.BestStart);
    }

    [Fact]
    public void Missing_air_quality_data_yields_nothing()
    {
        Assert.Null(Run(Feature(), Now, ForecastOnly(Day())));
    }

    [Fact]
    public void Night_hours_and_extreme_weather_score_zero()
    {
        var weather = Uniform(point => point with { IsDay = 0.0, Temperature = 22.0 });
        var result = Run(Feature(), Now, Combined(weather, CleanAir()));

        var score = Assert.IsType<OutdoorScoreResult>(result);
        Assert.Equal(0, score.Score);
    }

    [Fact]
    public void High_uv_and_pollen_reduce_score()
    {
        var weather = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0, UvIndex = 10.0 });
        var air = CleanAir(shape: (_, p) => p with { BirchPollen = 80.0 });

        var score = Assert.IsType<OutdoorScoreResult>(Run(Feature(), Now, Combined(weather, air)));
        Assert.True(score.Score < 85, $"Expected reduced score but got {score.Score}");
    }

    [Fact]
    public void Rain_and_high_probability_reduce_score()
    {
        var weather = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0, Precipitation = 1.0, PrecipitationProbability = 80.0 });
        var perfect = Uniform(point => point with { Temperature = 22.0, WindSpeed = 2.0, CloudCover = 10.0 });

        var rainyScore = Assert.IsType<OutdoorScoreResult>(Run(Feature(), Now, Combined(weather, CleanAir())));
        var perfectScore = Assert.IsType<OutdoorScoreResult>(Run(Feature(), Now, Combined(perfect, CleanAir())));
        Assert.True(rainyScore.Score < perfectScore.Score,
            $"Rainy ({rainyScore.Score}) should score lower than perfect ({perfectScore.Score})");
    }
}
