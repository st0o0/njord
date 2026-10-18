using Njord.Alerts.Rules;
using Njord.Consensus.Querying;
using Njord.Messages.Alerts;
using Njord.Model;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class FrostRuleSpec
{
    private static readonly FrostRule Rule = new();

    [Fact]
    public void Frost_all_models_agree()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, -2.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -1.0)),
            MakeForecast(new WeatherModel("m3"), (Temperature, -3.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
    }

    [Fact]
    public void Frost_no_model_agrees()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, 5.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 3.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
    }

    [Fact]
    public void Frost_partial_agreement()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, -3.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -1.0)),
            MakeForecast(new WeatherModel("m3"), (Temperature, 1.0)),
            MakeForecast(new WeatherModel("m4"), (Temperature, 5.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
        Assert.Equal(0.5, alert.Confidence);
    }

    [Fact]
    public void Frost_moderate_produces_orange()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, -8.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -7.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Frost_severe_produces_red()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, -18.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -16.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Frost_rule_classifies_the_24h_minimum_against_the_passed_thresholds()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, -3.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -3.0)));
        var settings = Defaults with { Frost = new SeverityThresholds(0, -2, -5, ScanDirection.Min) };

        var alert = Rule.Evaluate(consensus, settings);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
        Assert.Equal(1.0, alert.Confidence);
        Assert.Equal(-3.0, Assert.IsType<FrostDetails>(alert.Details).ExpectedLow);
    }

    [Fact]
    public void Lower_warning_threshold_applies_on_the_next_evaluation()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (Temperature, -3.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -3.0)));
        var first = Defaults with { Frost = new SeverityThresholds(0, -2, -5, ScanDirection.Min) };
        var second = Defaults with { Frost = new SeverityThresholds(0, -4, -5, ScanDirection.Min) };

        var before = Rule.Evaluate(consensus, first);
        var after = Rule.Evaluate(consensus, second);

        Assert.Equal(AlertSeverity.Orange, before.Severity);
        Assert.Equal(AlertSeverity.Yellow, after.Severity);
    }

    [Fact]
    public void Frost_reports_earliest_hour_and_agreeing_models()
    {
        var consensus = Identical(2, _ => [(Temperature, -3.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        var details = Assert.IsType<FrostDetails>(alert.Details);
        Assert.Multiple(
            () => Assert.Equal(Horizon.Hours(0), details.EarliestFrost),
            () => Assert.Equal(2, details.ModelsAgreeing),
            () => Assert.Equal(0, alert.HoursUntil),
            () => Assert.Equal(0.0, alert.Threshold));
    }
}
