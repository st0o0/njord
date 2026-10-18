using Njord.Indices.Scores;
using Njord.Messages.Indices;

namespace Njord.Indices.Tests.Scores;

public sealed class CyclingScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void CyclingComfort_calm_warm() =>
        Assert.InRange(CyclingScore.Compute(18, 50, 1.5, 0, Prefs), 80, 100);

    [Fact]
    public void CyclingComfort_very_windy() =>
        Assert.InRange(CyclingScore.Compute(18, 50, 12, 0, Prefs), 0, 65);
}
