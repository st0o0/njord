using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class FogRuleSpec
{
    private static readonly FogRule Rule = new();

    [Fact]
    public void Fog_turns_orange_when_it_persists_for_the_configured_hours()
    {
        var alert = Rule.Evaluate(Day((h, p) => h < 4 ? p with { Temperature = 5.0, DewPoint = 4.5, WindSpeed = 1.0, Humidity = 95.0 } : p), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Fog, alert.Type),
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity));
    }

    [Fact]
    public void Fog_is_not_raised_when_dew_point_spread_is_wide()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { Temperature = 5.0, DewPoint = 1.0, WindSpeed = 1.0, Humidity = 95.0 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
