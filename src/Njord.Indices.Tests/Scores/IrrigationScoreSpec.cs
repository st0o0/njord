using Njord.Indices.Scores;
using Njord.Messages.Indices;

namespace Njord.Indices.Tests.Scores;

public sealed class IrrigationScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void IrrigationNeed_hot_dry() =>
        Assert.InRange(IrrigationScore.Compute(0, 32, 30, 6, Prefs), 85, 100);

    [Fact]
    public void IrrigationNeed_rainy() =>
        Assert.InRange(IrrigationScore.Compute(90, 15, 80, 1, Prefs), 0, 15);
}
