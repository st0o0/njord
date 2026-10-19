using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class ThunderstormRuleSpec
{
    private static readonly ThunderstormRule Rule = new();

    [Fact]
    public void Thunderstorm_is_red_when_all_hours_meet_every_condition()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Cape = 1001.0, Precipitation = 5.1, WindGusts = 15.1 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Thunderstorm, alert.Type),
            () => Assert.Equal(AlertSeverity.Red, alert.Severity));
    }

    [Fact]
    public void Thunderstorm_is_not_raised_when_cape_does_not_exceed_its_threshold()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Cape = 1000.0, Precipitation = 5.1, WindGusts = 15.1 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
