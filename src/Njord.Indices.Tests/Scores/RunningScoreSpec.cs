using Njord.Compute.Contracts;
using Njord.Indices.Scores;

namespace Njord.Indices.Tests.Scores;

public sealed class RunningScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void RunningComfort_ideal() =>
        Assert.InRange(RunningScore.Compute(12, 45, 2, 0, Prefs), 85, 100);

    [Fact]
    public void RunningComfort_hot_humid() =>
        Assert.InRange(RunningScore.Compute(35, 80, 0.5, 10, Prefs), 0, 55);

    [Fact]
    public void Running_custom_temp_range()
    {
        var cold = Prefs with { IdealTempLow = 0, IdealTempHigh = 15 };
        var scoreCustom = RunningScore.Compute(3, 50, 2, 0, cold);
        var scoreDefault = RunningScore.Compute(3, 50, 2, 0, Prefs);
        Assert.True(scoreCustom > scoreDefault, "Wider cold range should accept 3°C better");
    }
}
