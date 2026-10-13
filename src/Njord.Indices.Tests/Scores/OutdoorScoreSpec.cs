using Njord.Compute.Contracts;
using Njord.Indices.Scores;

namespace Njord.Indices.Tests.Scores;

public sealed class OutdoorScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void OutdoorScore_pleasant_spring() =>
        Assert.InRange(OutdoorScore.Compute(22, 50, 5, 3, 20, Prefs), 80, 100);

    [Fact]
    public void OutdoorScore_stormy_winter() =>
        Assert.InRange(OutdoorScore.Compute(2, 90, 90, 12, 100, Prefs), 0, 10);

    [Fact]
    public void OutdoorScore_schwuel_day_scores_low()
    {
        var score = OutdoorScore.Compute(33, 85, 10, 0.5, 30, Prefs);
        Assert.InRange(score, 0, 40);
    }

    [Fact]
    public void OutdoorScore_schwuel_day_with_high_sensitivity_scores_lower()
    {
        var highSens = Prefs with { HeatSensitivity = 1.5, HumiditySensitivity = 1.3 };
        var score = OutdoorScore.Compute(33, 85, 10, 0.5, 30, highSens);
        Assert.InRange(score, 0, 32);
    }

    [Fact]
    public void OutdoorScore_shifted_ideal_temp()
    {
        var shifted = Prefs with { IdealTemp = 26.0 };
        var scoreAt26 = OutdoorScore.Compute(26, 45, 5, 3, 20, shifted);
        var scoreAt22 = OutdoorScore.Compute(26, 45, 5, 3, 20, Prefs);
        Assert.True(scoreAt26 > scoreAt22, "Shifted ideal should yield higher score at 26°C");
        Assert.InRange(scoreAt26, 85, 100);
    }

    [Fact]
    public void BreezeScore_ideal_range_scores_100()
    {
        Assert.Equal(100, OutdoorScore.Breeze(3, 1.0));
    }

    [Fact]
    public void BreezeScore_windstill_penalized()
    {
        var score = OutdoorScore.Breeze(0, 1.0);
        Assert.InRange(score, 20, 40);
    }

    [Fact]
    public void BreezeScore_strong_wind_penalized()
    {
        var score = OutdoorScore.Breeze(10, 1.0);
        Assert.InRange(score, 0, 40);
    }

    [Fact]
    public void Higher_heat_sensitivity_lowers_outdoor_score_in_heat()
    {
        var normal = OutdoorScore.Compute(32, 50, 5, 3, 30, Prefs);
        var sensitive = OutdoorScore.Compute(32, 50, 5, 3, 30, Prefs with { HeatSensitivity = 2.0 });
        Assert.True(sensitive < normal, "Higher heat sensitivity should produce lower score in heat");
    }
}
