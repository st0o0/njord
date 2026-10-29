using Microsoft.Extensions.Options;
using Njord.Features.Scores.AllergySeverity;
using Njord.Messages.Results.Scores;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores.AllergySeverity;

public sealed class AllergySeverityScoreTests
{
    private static AllergySeverityScore Feature() => new(Options.Create(new AllergySeverityConfig { Enabled = true }));

    private static int ScoreFor(Func<Njord.Core.Forecast.ForecastPoint, Njord.Core.Forecast.ForecastPoint> weatherChange, double birch = 60.0)
    {
        var weather = Uniform(weatherChange);
        var air = CleanAir(shape: (_, p) => p with { BirchPollen = birch });
        return Assert.IsType<AllergySeverityScoreResult>(Run(Feature(), Now, Combined(weather, air))).Score;
    }

    [Fact]
    public void High_pollen_in_neutral_weather_scores_the_normalised_pollen_level()
    {
        Assert.Equal(60, ScoreFor(p => p with { WindSpeed = 2.0, Humidity = 60.0, BoundaryLayerHeight = 1500.0, Rain = 0.0 }));
    }

    [Fact]
    public void Pollen_above_the_reference_is_clamped_to_100()
    {
        Assert.Equal(100, ScoreFor(p => p with { WindSpeed = 2.0, Humidity = 60.0, BoundaryLayerHeight = 1500.0, Rain = 0.0 }, birch: 400.0));
    }

    [Fact]
    public void Dry_wind_a_shallow_boundary_layer_and_low_humidity_amplify_the_score()
    {
        Assert.Equal(100, ScoreFor(p => p with { WindSpeed = 6.0, Humidity = 30.0, BoundaryLayerHeight = 400.0, Rain = 0.0 }));
    }

    [Fact]
    public void Rain_dampens_the_score()
    {
        Assert.Equal(30, ScoreFor(p => p with { WindSpeed = 2.0, Humidity = 60.0, BoundaryLayerHeight = 1500.0, Rain = 1.0 }));
    }

    [Fact]
    public void Humid_air_dampens_the_score()
    {
        Assert.Equal(45, ScoreFor(p => p with { WindSpeed = 2.0, Humidity = 90.0, BoundaryLayerHeight = 1500.0, Rain = 0.0 }));
    }

    [Fact]
    public void Dampeners_never_push_the_score_below_zero()
    {
        Assert.Equal(0, ScoreFor(
            p => p with { WindSpeed = 2.0, Humidity = 90.0, BoundaryLayerHeight = 1500.0, Rain = 2.0 }, birch: 20.0));
    }

    [Fact]
    public void A_low_pollen_stretch_is_reported_as_the_best_window()
    {
        var weather = Uniform(p => p with { WindSpeed = 2.0, Humidity = 60.0, BoundaryLayerHeight = 1500.0, Rain = 0.0 });
        var air = CleanAir(shape: (hour, p) => p with { BirchPollen = hour < 6 ? 90.0 : 5.0 });

        var score = Assert.IsType<AllergySeverityScoreResult>(Run(Feature(), Now, Combined(weather, air)));

        Assert.Multiple(
            () => Assert.Equal(90, score.Score),
            () => Assert.Equal(Now.AddHours(6), score.BestStart),
            () => Assert.Equal(18, score.BestHours),
            () => Assert.NotEmpty(score.Windows));
    }

    [Fact]
    public void Missing_pollen_data_produces_no_score()
    {
        var weather = Uniform(p => p with { WindSpeed = 2.0 });

        Assert.Null(Run(Feature(), Now, Combined(weather, CleanAir())));
    }
}
