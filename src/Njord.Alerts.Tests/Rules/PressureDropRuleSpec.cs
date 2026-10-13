using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class PressureDropRuleSpec
{
    private static readonly PressureDropRule Rule = new();

    [Fact]
    public void PressureDrop_front_approaching()
    {
        static (ParameterDef, double)[] Pressure(int h) => [(PressureMsl, 1020.0 - (h < 6 ? h * 2.5 : 0))];
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), Pressure),
            MakeForecast(new WeatherModel("m2"), Pressure));

        var alert = Rule.Evaluate(consensus, Defaults with { PressureDropThreshold = 5.0, PressureDropSevereThreshold = 15.0 });

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void PressureDrop_stable()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (PressureMsl, 1015.0)));

        var alert = Rule.Evaluate(consensus, Defaults with { PressureDropThreshold = 5.0 });

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void PressureDrop_severe_produces_orange()
    {
        static (ParameterDef, double)[] Pressure(int h) => [(PressureMsl, 1020.0 - (h < 4 ? h * 4.0 : 0))];
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), Pressure),
            MakeForecast(new WeatherModel("m2"), Pressure));

        var alert = Rule.Evaluate(consensus, Defaults with { PressureDropThreshold = 5.0, PressureDropSevereThreshold = 10.0 });

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }
}
