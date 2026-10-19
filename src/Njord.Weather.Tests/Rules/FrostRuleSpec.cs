using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class FrostRuleSpec
{
    private static readonly FrostRule Rule = new();

    [Fact]
    public void Frost_is_raised_at_zero_degrees()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 0.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Frost, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Frost_is_not_raised_above_zero_degrees()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 0.1 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
