using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class VisibilityRuleSpec
{
    private static readonly VisibilityRule Rule = new();

    [Fact]
    public void Visibility_is_raised_below_1000_m()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Visibility = 999.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Visibility, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Visibility_is_not_raised_at_exactly_1000_m()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Visibility = 1000.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
