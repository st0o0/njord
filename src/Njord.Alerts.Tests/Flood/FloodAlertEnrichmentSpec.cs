using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.Flood;
using Njord.Messages.Alerts;

namespace Njord.Alerts.Tests.Flood;

using static Njord.Alerts.Tests.Flood.FloodFixtures;

public sealed class FloodAlertEnrichmentSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    [Fact]
    public void The_risk_enrichment_raises_an_event_with_location_and_time()
    {
        var input = new FloodRiskInput(Lucerne, Flood(Point(0, 160)));

        var raised = Assert.IsType<FloodAlertRaisedEvent>(
            Assert.Single(new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), true).Evaluate(input, Time)));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp),
            () => Assert.Equal(FloodAlertType.FloodRisk, raised.Alert.Type),
            () => Assert.Equal(AlertSeverity.Orange, raised.Alert.Severity));
    }

    [Fact]
    public void The_risk_enrichment_stays_silent_below_the_thresholds_and_when_disabled()
    {
        var calm = new FloodRiskInput(Lucerne, Flood(Point(0, 100)));
        var wild = new FloodRiskInput(Lucerne, Flood(Point(0, 500)));

        Assert.Multiple(
            () => Assert.Empty(new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), true).Evaluate(calm, Time)),
            () => Assert.Empty(new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), false).Evaluate(wild, Time)));
    }

    [Fact]
    public void The_rain_enrichment_raises_a_flood_rain_event_unless_disabled()
    {
        var input = new FloodRainInput(Lucerne, Flood(Point(0, 110), Point(1, 130)), Rain(1.5));

        var raised = Assert.IsType<FloodAlertRaisedEvent>(
            Assert.Single(new FloodRainAlertEnrichment(new FloodRainAlertRule(), true).Evaluate(input, Time)));

        Assert.Multiple(
            () => Assert.Equal(FloodAlertType.FloodRain, raised.Alert.Type),
            () => Assert.Empty(new FloodRainAlertEnrichment(new FloodRainAlertRule(), false).Evaluate(input, Time)));
    }
}
