using Njord.Alerts.Rules;
using Njord.Messages.Alerts;
using Njord.Model;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests.Rules;

public sealed class VisibilityRuleSpec
{
    private static readonly VisibilityRule Rule = new();

    [Fact]
    public void Visibility_reduced_produces_yellow()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 800.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 900.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Yellow, alert.Severity);
    }

    [Fact]
    public void Visibility_dense_fog_produces_orange()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 150.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 180.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Orange, alert.Severity);
    }

    [Fact]
    public void Visibility_near_zero_produces_red()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 30.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 40.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.Red, alert.Severity);
    }

    [Fact]
    public void Visibility_good_produces_none()
    {
        var consensus = ConsensusOf(
            MakeForecast(new WeatherModel("m1"), (VisibilityParam, 5000.0)),
            MakeForecast(new WeatherModel("m2"), (VisibilityParam, 8000.0)));

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Equal(AlertSeverity.None, alert.Severity);
    }

    [Fact]
    public void Visibility_classifies_the_24h_minimum_not_the_maximum()
    {
        var consensus = Identical(2, h => [(VisibilityParam, h < 12 ? 5000.0 : 150.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, alert.Severity),
            () => Assert.Equal(150.0, alert.TriggerValue),
            () => Assert.Equal(200.0, alert.Threshold),
            () => Assert.Equal(0.5, alert.Confidence),
            () => Assert.Equal(new VisibilityDetails(150.0, 12), alert.Details));
    }

    [Fact]
    public void Visibility_confidence_is_the_share_of_hours_below_the_advisory_threshold()
    {
        var consensus = Identical(2, h => [(VisibilityParam, h < 8 ? 500.0 : 5000.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Yellow, alert.Severity),
            () => Assert.Equal(0.333, alert.Confidence),
            () => Assert.Equal(new VisibilityDetails(500.0, 8), alert.Details));
    }

    [Fact]
    public void Visibility_exactly_at_the_advisory_threshold_is_not_reduced()
    {
        var consensus = Identical(2, _ => [(VisibilityParam, 1000.0)]);

        var alert = Rule.Evaluate(consensus, Defaults);

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.Confidence),
            () => Assert.Equal(1000.0, alert.TriggerValue),
            () => Assert.Equal(1000.0, alert.Threshold),
            () => Assert.Equal(new VisibilityDetails(1000.0, 0), alert.Details));
    }
}
