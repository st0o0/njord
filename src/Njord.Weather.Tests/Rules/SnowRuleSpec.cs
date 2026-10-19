using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class SnowRuleSpec
{
    private static readonly SnowRule Rule = new();

    [Fact]
    public void Snow_is_orange_above_5_cm_accumulation()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Snowfall = h == 0 ? 6.0 : 0.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Snow, alert.Type),
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity));
    }

    [Fact]
    public void Snow_is_not_raised_without_accumulation()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Snowfall = 0.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
