using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class IceRuleSpec
{
    private static readonly IceRule Rule = new();

    [Fact]
    public void Ice_is_raised_when_it_rains_at_the_ice_threshold()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Precipitation = 1.0, Temperature = 2.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Ice, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Ice_is_not_raised_when_rain_falls_above_the_ice_threshold()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Precipitation = 1.0, Temperature = 2.1 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
