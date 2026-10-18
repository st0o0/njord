using Njord.Consensus;
using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
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
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), Pressure),
            MakeForecast(new WeatherModel("m2"), Pressure));

        var alert = Rule.Evaluate(consensus, Defaults with { PressureDropThreshold = 5.0, PressureDropSevereThreshold = 15.0 });

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void PressureDrop_stable()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (PressureMsl, 1015.0)));

        var alert = Rule.Evaluate(consensus, Defaults with { PressureDropThreshold = 5.0 });

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void PressureDrop_severe_produces_orange()
    {
        static (ParameterDef, double)[] Pressure(int h) => [(PressureMsl, 1020.0 - (h < 4 ? h * 4.0 : 0))];
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), Pressure),
            MakeForecast(new WeatherModel("m2"), Pressure));

        var alert = Rule.Evaluate(consensus, Defaults with { PressureDropThreshold = 5.0, PressureDropSevereThreshold = 10.0 });

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    private static LocationConsensus DropAtHourThree(double drop) =>
        Identical(2, h => [(PressureMsl, h >= 3 ? 1020.0 - drop : 1020.0)]);

    [Theory]
    [InlineData(3.0, AlertSeverity.None, 5.0, 0.0)]
    [InlineData(5.0, AlertSeverity.Yellow, 5.0, 1.0)]
    [InlineData(7.5, AlertSeverity.Yellow, 5.0, 1.0)]
    [InlineData(10.0, AlertSeverity.Orange, 10.0, 1.0)]
    [InlineData(12.0, AlertSeverity.Orange, 10.0, 1.0)]
    public void PressureDrop_classifies_the_drop_against_both_thresholds_inclusively(
        double drop, AlertSeverity severity, double threshold, double confidence)
    {
        var alert = Rule.Evaluate(DropAtHourThree(drop), Defaults);

        Assert.Multiple(
            () => Assert.Equal(severity, alert.Severity),
            () => Assert.Equal(drop, alert.TriggerValue),
            () => Assert.Equal(threshold, alert.Threshold),
            () => Assert.Equal(confidence, alert.Confidence),
            () => Assert.Equal(drop, Assert.IsType<PressureDropDetails>(alert.Details).MaxDrop));
    }

    [Fact]
    public void PressureDrop_below_the_warning_threshold_but_above_the_severe_one_keeps_the_warning_threshold()
    {
        var settings = Defaults with { PressureDropThreshold = 8.0, PressureDropSevereThreshold = 5.0 };

        var alert = Rule.Evaluate(DropAtHourThree(6.0), settings);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(8.0, alert.Threshold),
            () => Assert.Equal(0.0, alert.Confidence));
    }

    [Fact]
    public void PressureDrop_evaluates_a_series_of_exactly_four_hours()
    {
        var consensus = ConsensusOf(
            MakeForecastOver(new WeatherModel("m1"), 4, h => [(PressureMsl, h == 3 ? 1013.0 : 1020.0)]),
            MakeForecastOver(new WeatherModel("m2"), 4, h => [(PressureMsl, h == 3 ? 1013.0 : 1020.0)]));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(7.0, alert.TriggerValue);
    }

    [Fact]
    public void PressureDrop_ignores_a_short_dip_between_neighbouring_hours()
    {
        var consensus = Identical(2, h => [(PressureMsl, h == 2 ? 1012.0 : 1020.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.TriggerValue));
    }
}
