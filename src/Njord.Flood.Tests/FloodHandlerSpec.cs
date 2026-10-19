using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;
using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    private static FloodRiskHandler Risk(bool enabled = true) => new(new FloodRiskAlertRule(), AlertRuleFixture.OptionsWith(enabled));

    private static FloodRainHandler Rainfall(bool enabled = true) => new(new FloodRainAlertRule(), AlertRuleFixture.OptionsWith(enabled));

    [Fact]
    public void The_risk_handler_triggers_only_on_flood()
    {
        Assert.Equal([DomainKind.Flood], Risk().Triggers);
    }

    [Fact]
    public void The_rain_handler_triggers_on_flood_and_weather()
    {
        var triggers = Rainfall().Triggers;

        Assert.Multiple(
            () => Assert.Equal(2, triggers.Count),
            () => Assert.Contains(DomainKind.Flood, triggers),
            () => Assert.Contains(DomainKind.Weather, triggers));
    }

    [Fact]
    public void The_risk_handler_raises_a_flood_risk_alert()
    {
        var current = new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 500)));

        var raised = Assert.IsType<PipelineEvent.AlertRaised>(Assert.Single(Risk().Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(AlertType.FloodRisk, raised.Type),
            () => Assert.Equal(AlertSeverity.Red, raised.Severity),
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp));
    }

    [Fact]
    public void The_risk_handler_is_quiet_without_flood_data_for_calm_water_or_when_disabled()
    {
        var calm = new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 100)));
        var wild = new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 500)));

        Assert.Multiple(
            () => Assert.Empty(Risk().Handle(new LocationForecast(Lucerne), null, Time)),
            () => Assert.Empty(Risk().Handle(calm, null, Time)),
            () => Assert.Empty(Risk(enabled: false).Handle(wild, null, Time)));
    }

    [Fact]
    public void The_rain_handler_raises_a_flood_rain_alert_from_both_domains()
    {
        var current = new LocationForecast(Lucerne, Weather: Rain(1.5), Flood: FloodForecast(Point(0, 110), Point(1, 130)));

        var raised = Assert.IsType<PipelineEvent.AlertRaised>(Assert.Single(Rainfall().Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(AlertType.FloodRain, raised.Type),
            () => Assert.Equal(AlertSeverity.Orange, raised.Severity),
            () => Assert.Equal(Now, raised.Timestamp));
    }

    [Fact]
    public void The_rain_handler_needs_both_domains_and_respects_the_toggle()
    {
        var flood = FloodForecast(Point(0, 110), Point(1, 130));
        var both = new LocationForecast(Lucerne, Weather: Rain(1.5), Flood: flood);

        Assert.Multiple(
            () => Assert.Empty(Rainfall().Handle(new LocationForecast(Lucerne, Flood: flood), null, Time)),
            () => Assert.Empty(Rainfall().Handle(new LocationForecast(Lucerne, Weather: Rain(1.5)), null, Time)),
            () => Assert.Empty(Rainfall(enabled: false).Handle(both, null, Time)));
    }
}
