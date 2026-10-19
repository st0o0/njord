using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class StormRuleSpec
{
    private static readonly StormRule Rule = new();

    [Fact]
    public void Storm_is_raised_at_17_m_per_s_gusts()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { WindGusts = 17.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Storm, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Storm_is_not_raised_below_17_m_per_s_gusts()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { WindGusts = 16.9 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
