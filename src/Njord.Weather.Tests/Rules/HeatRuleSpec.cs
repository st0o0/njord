using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class HeatRuleSpec
{
    private static readonly HeatRule Rule = new();

    [Fact]
    public void Heat_is_raised_at_30_degrees()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 30.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Heat, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Heat_is_not_raised_below_30_degrees()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 29.9 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
