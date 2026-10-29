using Microsoft.Extensions.Options;
using Njord.Core.AirQuality;
using Njord.Core.Forecast;
using Njord.Features.Scores.AsthmaRisk;
using Njord.Messages.Pipeline;
using static Njord.Tests.Shared.AlertRuleFixture;
using static Njord.Tests.Shared.FeatureRunner;
using static Njord.Tests.Shared.LifestyleFixture;

namespace Njord.Features.Tests.Scores.AsthmaRisk;

public sealed class AsthmaRiskScoreTests
{
    private static AsthmaRiskScore Feature() => new(Options.Create(new AsthmaRiskConfig { Enabled = true }));

    private static ForecastPoint Calm(ForecastPoint p) => p with { Humidity = 50.0, BoundaryLayerHeight = 2000.0 };

    private static AirQualityPoint Clear(AirQualityPoint p) => p with { Pm25 = 0.0, Ozone = 0.0, BirchPollen = 0.0, GrassPollen = 0.0 };

    private static int ScoreFor(Func<ForecastPoint, ForecastPoint> weather, Func<AirQualityPoint, AirQualityPoint> air)
    {
        var forecast = Uniform(p => weather(Calm(p)));
        var quality = CleanAir(shape: (_, p) => air(Clear(p)));
        return Assert.IsType<AsthmaRiskScoreResult>(Run(Feature(), Now, Combined(forecast, quality))).Score;
    }

    [Fact]
    public void Clean_calm_air_scores_zero()
    {
        Assert.Equal(0, ScoreFor(p => p, a => a));
    }

    [Theory]
    [InlineData(75.0, 35)]
    [InlineData(150.0, 35)]
    [InlineData(37.5, 18)]
    public void Fine_particles_weigh_35_percent(double pm25, int expected)
    {
        Assert.Equal(expected, ScoreFor(p => p, a => a with { Pm25 = pm25 }));
    }

    [Fact]
    public void Ozone_weighs_25_percent()
    {
        Assert.Equal(25, ScoreFor(p => p, a => a with { Ozone = 180.0 }));
    }

    [Fact]
    public void The_strongest_pollen_species_weighs_20_percent()
    {
        Assert.Equal(20, ScoreFor(p => p, a => a with { BirchPollen = 30.0, GrassPollen = 100.0 }));
    }

    [Theory]
    [InlineData(50.0, 0)]
    [InlineData(20.0, 10)]
    [InlineData(85.0, 10)]
    [InlineData(70.0, 5)]
    public void Humidity_follows_a_u_curve_around_40_to_60_percent(double humidity, int expected)
    {
        Assert.Equal(expected, ScoreFor(p => p with { Humidity = humidity }, a => a));
    }

    [Theory]
    [InlineData(2000.0, 0)]
    [InlineData(1100.0, 5)]
    [InlineData(200.0, 10)]
    public void A_shallow_boundary_layer_raises_the_score(double height, int expected)
    {
        Assert.Equal(expected, ScoreFor(p => p with { BoundaryLayerHeight = height }, a => a));
    }

    [Fact]
    public void Every_factor_at_its_limit_scores_100()
    {
        var score = ScoreFor(
            p => p with { Humidity = 90.0, BoundaryLayerHeight = 200.0 },
            a => a with { Pm25 = 80.0, Ozone = 200.0, BirchPollen = 150.0 });

        Assert.Equal(100, score);
    }

    [Fact]
    public void Cold_damp_air_amplifies_the_score()
    {
        var score = ScoreFor(
            p => p with { Temperature = 4.0, Humidity = 85.0 },
            a => a with { Pm25 = 75.0, Ozone = 180.0 });

        Assert.Equal(91, score);
    }

    [Fact]
    public void A_rapid_temperature_change_amplifies_the_score()
    {
        var forecast = Day((hour, p) => Calm(p) with { Temperature = hour < 3 ? 15.0 : 25.0 });
        var quality = CleanAir(shape: (_, p) => Clear(p) with { Pm25 = 75.0, Ozone = 180.0 });

        var score = Assert.IsType<AsthmaRiskScoreResult>(Run(Feature(), Now, Combined(forecast, quality)));

        Assert.Equal(72, score.Score);
    }

    [Fact]
    public void The_low_risk_stretch_is_reported_as_the_best_window()
    {
        var forecast = Uniform(Calm);
        var quality = CleanAir(shape: (hour, p) => hour < 6 ? Clear(p) with { Pm25 = 75.0, Ozone = 180.0 } : Clear(p));

        var score = Assert.IsType<AsthmaRiskScoreResult>(Run(Feature(), Now, Combined(forecast, quality)));

        Assert.Multiple(
            () => Assert.Equal(60, score.Score),
            () => Assert.Equal(Now.AddHours(6), score.BestStart),
            () => Assert.Equal(18, score.BestHours),
            () => Assert.NotEmpty(score.Windows));
    }

    [Fact]
    public void Missing_air_quality_data_produces_no_score()
    {
        Assert.Null(Run(Feature(), Now, Combined(Uniform(Calm), CleanAir(0))));
    }
}
