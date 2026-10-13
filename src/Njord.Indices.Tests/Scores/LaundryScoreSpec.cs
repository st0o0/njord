using Njord.Compute.Contracts;
using Njord.Indices.Scores;

namespace Njord.Indices.Tests.Scores;

public sealed class LaundryScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void LaundryDrying_perfect_day() =>
        Assert.InRange(LaundryScore.Compute(28, 35, 5, 0, 100, Prefs), 90, 100);

    [Fact]
    public void LaundryDrying_cold_rainy_day() =>
        Assert.InRange(LaundryScore.Compute(5, 90, 1, 80, 0, Prefs), 0, 15);

    [Fact]
    public void Higher_humidity_sensitivity_lowers_laundry_score()
    {
        var normal = LaundryScore.Compute(20, 55, 3, 10, 60, Prefs);
        var sensitive = LaundryScore.Compute(20, 55, 3, 10, 60, Prefs with { HumiditySensitivity = 2.0 });
        Assert.True(sensitive < normal, "Higher humidity sensitivity should produce lower score");
    }
}
