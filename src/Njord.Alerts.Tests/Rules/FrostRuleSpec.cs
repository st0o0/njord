using Njord.Alerts.Rules;
using Njord.Compute.Contracts;
using Njord.Weather;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class FrostRuleSpec
{
    private static readonly FrostRule Rule = new();

    [Fact]
    public void Frost_all_models_agree()
    {
        var consensus = Consensus(
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
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, 5.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, 3.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
        Assert.Equal(0.0, alert.Confidence);
    }

    [Fact]
    public void Frost_partial_agreement()
    {
        var consensus = Consensus(
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
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -8.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -7.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Frost_severe_produces_red()
    {
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -18.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -16.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Frost_rule_classifies_the_24h_minimum_against_the_passed_thresholds()
    {
        var consensus = Consensus(
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
        var consensus = Consensus(
            MakeForecast(new WeatherModel("m1"), (Temperature, -3.0)),
            MakeForecast(new WeatherModel("m2"), (Temperature, -3.0)));
        var first = Defaults with { Frost = new SeverityThresholds(0, -2, -5, ScanDirection.Min) };
        var second = Defaults with { Frost = new SeverityThresholds(0, -4, -5, ScanDirection.Min) };

        var before = Rule.Evaluate(consensus, first);
        var after = Rule.Evaluate(consensus, second);

        Assert.Equal(AlertSeverity.Orange, before.Severity);
        Assert.Equal(AlertSeverity.Yellow, after.Severity);
    }
}
