using Njord.Compute.Contracts;
using Njord.Indices.Scores;

namespace Njord.Indices.Tests.Scores;

public sealed class NightVentilationScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void NightVentilation_cool_evening() =>
        Assert.InRange(NightVentilationScore.Compute(17, 45, 3, 0, Prefs), 75, 100);

    [Fact]
    public void NightVentilation_hot_humid() =>
        Assert.InRange(NightVentilationScore.Compute(30, 80, 1, 0, Prefs), 0, 35);

    [Fact]
    public void NightVentilation_custom_indoor_temp()
    {
        var warm = Prefs with { IndoorTemp = 25.0 };
        var scoreCoolOutdoor = NightVentilationScore.Compute(15, 45, 3, 0, warm);
        var scoreDefault = NightVentilationScore.Compute(15, 45, 3, 0, Prefs);
        Assert.True(scoreCoolOutdoor > scoreDefault, "Higher indoor temp with cool outdoor should increase night ventilation score");
    }
}
