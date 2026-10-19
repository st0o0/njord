using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class HumidityRuleSpec
{
    private static readonly HumidityRule Rule = new();

    [Fact]
    public void Humidity_is_raised_at_a_dew_point_of_16()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { IsDay = true, DewPoint = 16.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Humidity, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Humidity_is_not_raised_below_a_dew_point_of_16()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { IsDay = true, DewPoint = 15.9 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
