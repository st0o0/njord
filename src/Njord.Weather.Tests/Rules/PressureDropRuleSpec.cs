using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class PressureDropRuleSpec
{
    private static readonly PressureDropRule Rule = new();

    [Fact]
    public void Pressure_drop_is_raised_at_5_hpa_within_3_hours()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Pressure = h < 3 ? 1013.0 : 1008.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.PressureDrop, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Pressure_drop_is_not_raised_below_5_hpa()
    {
        var alert = Rule.Evaluate(Day((h, p) => p with { Pressure = h < 3 ? 1013.0 : 1008.1 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
