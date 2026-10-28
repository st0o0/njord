using Microsoft.Extensions.Options;
using Njord.Core.Forecast;
using Njord.Features.Scores.SprayWindow;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores.SprayWindow;

public sealed class SprayWindowScoreTests
{
    private static SprayWindowScore Feature() => new(Options.Create(new SprayWindowConfig { Enabled = true }));

    private static ForecastPoint Calm(ForecastPoint p) => p with
    {
        WindSpeed = 1.0,
        Rain = 0.0,
        PrecipitationProbability = 0.0,
        Temperature = 18.0,
        Humidity = 60.0,
    };

    [Fact]
    public void Calm_dry_mild_day_scores_100_for_all_day()
    {
        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(Uniform(Calm))));

        Assert.Multiple(
            () => Assert.Equal(100, score.Score),
            () => Assert.Equal(Now, score.BestStart),
            () => Assert.Equal(24, score.BestHours),
            () => Assert.Single(score.Windows));
    }

    [Fact]
    public void Wind_above_the_limit_scores_zero_with_a_wind_limit()
    {
        var weather = Day((hour, p) => Calm(p) with { WindSpeed = hour >= 10 ? 6.0 : 1.0 });

        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        var window = Assert.Single(score.Windows);
        Assert.Multiple(
            () => Assert.Equal(Now, window.Start),
            () => Assert.Equal(Now.AddHours(10), window.End),
            () => Assert.NotNull(window.Limiting),
            () => Assert.Equal(LimitingKind.Wind, window.Limiting!.Kind));
    }

    [Fact]
    public void Wind_at_the_limit_is_still_allowed()
    {
        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(Uniform(p => Calm(p) with { WindSpeed = 3.0 }))));

        Assert.Equal(100, score.Score);
    }

    [Fact]
    public void Rain_within_the_rain_free_hours_blocks_the_hours_before_it()
    {
        var weather = Day((hour, p) => Calm(p) with { Rain = hour == 12 ? 1.0 : 0.0 });

        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        Assert.Multiple(
            () => Assert.Equal(2, score.Windows.Count),
            () => Assert.Equal(Now, score.Windows.Single(w => w.Start == Now).Start),
            () => Assert.Equal(Now.AddHours(9), score.Windows.Single(w => w.Start == Now).End),
            () => Assert.Contains(score.Windows, w => w.Start == Now.AddHours(13)));
    }

    [Fact]
    public void A_high_rain_probability_blocks_spraying()
    {
        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(Uniform(p => Calm(p) with { PrecipitationProbability = 20.0 }))));

        Assert.Multiple(
            () => Assert.Equal(0, score.Score),
            () => Assert.Equal(0, score.BestHours),
            () => Assert.Empty(score.Windows));
    }

    [Fact]
    public void Temperature_outside_the_range_lowers_the_score_linearly()
    {
        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(Uniform(p => Calm(p) with { Temperature = 5.0 }))));

        Assert.Equal(90, score.Score);
    }

    [Fact]
    public void A_cold_spell_ends_the_window_with_a_low_temperature_limit()
    {
        var weather = Day((hour, p) => Calm(p) with { Temperature = hour >= 6 ? -5.0 : 18.0, Humidity = hour >= 6 ? 90.0 : 60.0 });

        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(weather)));

        var window = Assert.Single(score.Windows);
        Assert.NotNull(window.Limiting);
        Assert.Multiple(
            () => Assert.Equal(Now.AddHours(6), window.End),
            () => Assert.Equal(LimitingKind.TemperatureLow, window.Limiting.Kind),
            () => Assert.Equal(-5.0, window.Limiting.Value));
    }

    [Fact]
    public void Humidity_outside_the_range_lowers_the_score()
    {
        var score = Assert.IsType<SprayWindowScoreResult>(Run(Feature(), Now, ForecastOnly(Uniform(p => Calm(p) with { Humidity = 90.0 }))));

        Assert.Equal(95, score.Score);
    }
}
