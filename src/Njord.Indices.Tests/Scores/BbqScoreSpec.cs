using Njord.Indices.Scores;
using Njord.Messages.Indices;

namespace Njord.Indices.Tests.Scores;

public sealed class BbqScoreSpec
{
    private static readonly ResolvedPreferences Prefs = ResolvedPreferences.Default;

    [Fact]
    public void BbqWeather_perfect() =>
        Assert.InRange(BbqScore.Compute(26, 40, 2, 0, Prefs), 90, 100);

    [Fact]
    public void BbqWeather_rain_kills_it() =>
        Assert.InRange(BbqScore.Compute(26, 40, 2, 80, Prefs), 0, 70);

    [Fact]
    public void Higher_rain_sensitivity_lowers_bbq_score()
    {
        var normal = BbqScore.Compute(25, 40, 2, 15, Prefs);
        var sensitive = BbqScore.Compute(25, 40, 2, 15, Prefs with { RainSensitivity = 2.0 });
        Assert.True(sensitive < normal, "Higher rain sensitivity should produce lower BBQ score");
    }

    [Fact]
    public void Bbq_custom_min_temp()
    {
        var strict = Prefs with { MinTemp = 15.0 };
        var scoreStrict = BbqScore.Compute(12, 40, 2, 0, strict);
        var scoreDefault = BbqScore.Compute(12, 40, 2, 0, Prefs);
        Assert.True(scoreStrict < scoreDefault, "Higher min temp should penalize 12°C more");
    }
}
