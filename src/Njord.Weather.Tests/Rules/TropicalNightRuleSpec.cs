using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class TropicalNightRuleSpec
{
    private static readonly TropicalNightRule Rule = new();

    [Fact]
    public void Tropical_night_is_raised_above_20_degrees()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { IsDay = false, Temperature = 20.1 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.TropicalNight, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Tropical_night_is_not_raised_at_exactly_20_degrees()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { IsDay = false, Temperature = 20.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
