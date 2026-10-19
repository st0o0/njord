using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class HeavyRainRuleSpec
{
    private static readonly HeavyRainRule Rule = new();

    [Fact]
    public void Heavy_rain_is_raised_at_the_hourly_threshold()
    {
        var alert = Rule.Evaluate(Day((h, p) => h == 0 ? p with { Precipitation = 10.0 } : p), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.HeavyRain, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Heavy_rain_is_not_raised_below_the_hourly_threshold()
    {
        var alert = Rule.Evaluate(Day((h, p) => h == 0 ? p with { Precipitation = 9.9 } : p), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
