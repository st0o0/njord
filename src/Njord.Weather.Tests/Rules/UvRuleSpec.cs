using Njord.Messages.Alerts;
using Njord.Weather.Alerts.Rules;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests.Rules;

public sealed class UvRuleSpec
{
    private static readonly UvRule Rule = new();

    [Fact]
    public void Uv_is_raised_at_index_3()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { UvIndex = 3.0 }), Defaults, Now);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Uv, alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity));
    }

    [Fact]
    public void Uv_is_not_raised_below_index_3()
    {
        var alert = Rule.Evaluate(Uniform(p => p with { UvIndex = 2.9 }), Defaults, Now);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }
}
