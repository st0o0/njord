using Njord.Compute.Contracts;
using Njord.Indices.Scores;

namespace Njord.Indices.Tests.Scores;

public sealed class SolarScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void SolarYield_clear_cool() =>
        Assert.InRange(SolarScore.Compute(800, 10, 18, Prefs), 85, 100);

    [Fact]
    public void SolarYield_overcast_hot() =>
        Assert.InRange(SolarScore.Compute(150, 90, 38, Prefs), 0, 20);
}
