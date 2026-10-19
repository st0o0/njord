using Microsoft.Extensions.Time.Testing;
using Njord.Alerts;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;

using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodAlertEnrichmentSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    [Fact]
    public void The_risk_enrichment_raises_an_event_with_location_and_time()
    {
        var input = new FloodRiskInput(Lucerne, FloodForecast(Point(0, 160)));

        var raised = Assert.IsType<AlertRaisedEvent>(
            Assert.Single(new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), AlertRuleFixture.Defaults, true).Evaluate(input, Time)));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp),
            () => Assert.Equal(AlertType.FloodRisk, raised.Alert.Type),
            () => Assert.Equal(AlertSeverity.Orange, raised.Alert.Severity));
    }

    [Fact]
    public void The_risk_enrichment_stays_silent_below_the_thresholds_and_when_disabled()
    {
        var calm = new FloodRiskInput(Lucerne, FloodForecast(Point(0, 100)));
        var wild = new FloodRiskInput(Lucerne, FloodForecast(Point(0, 500)));

        Assert.Multiple(
            () => Assert.Empty(new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), AlertRuleFixture.Defaults, true).Evaluate(calm, Time)),
            () => Assert.Empty(new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), AlertRuleFixture.Defaults, false).Evaluate(wild, Time)));
    }

    [Fact]
    public void The_rain_enrichment_raises_a_flood_rain_event_unless_disabled()
    {
        var input = new FloodRainInput(Lucerne, FloodForecast(Point(0, 110), Point(1, 130)), Rain(1.5));

        var raised = Assert.IsType<AlertRaisedEvent>(
            Assert.Single(new FloodRainAlertEnrichment(new FloodRainAlertRule(), AlertRuleFixture.Defaults, true).Evaluate(input, Time)));

        Assert.Multiple(
            () => Assert.Equal(AlertType.FloodRain, raised.Alert.Type),
            () => Assert.Empty(new FloodRainAlertEnrichment(new FloodRainAlertRule(), AlertRuleFixture.Defaults, false).Evaluate(input, Time)));
    }
}
