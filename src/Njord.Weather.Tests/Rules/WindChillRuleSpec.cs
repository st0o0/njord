using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class WindChillRuleSpec
{
    private static readonly WindChillRule Rule = new();

    [Fact]
    public void Wind_chill_is_raised_at_minus_10_degrees_felt()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { ApparentTemperature = -10.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.WindChill, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Wind_chill_is_not_raised_above_minus_10_degrees_felt()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { ApparentTemperature = -9.9 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
