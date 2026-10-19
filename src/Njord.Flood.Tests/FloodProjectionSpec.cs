using Microsoft.Extensions.Time.Testing;
using Njord.Alerts;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;

using static Njord.Flood.Tests.FloodFixtures;

namespace Njord.Flood.Tests;

public sealed class FloodProjectionSpec
{
    [Fact]
    public void The_risk_projector_requires_only_flood()
    {
        var projector = new FloodRiskProjector();
        var flood = FloodForecast(Point(0, 100));

        var input = projector.Project(new LocationForecast(Lucerne, Flood: flood), null);

        Assert.NotNull(input);
        Assert.Multiple(
            () => Assert.Equal([DomainKind.Flood], projector.RequiredDomains),
            () => Assert.Same(flood, input.Flood),
            () => Assert.Null(projector.Project(new LocationForecast(Lucerne), null)));
    }

    [Fact]
    public void The_rain_projector_requires_flood_and_weather()
    {
        var projector = new FloodRainProjector();
        var flood = FloodForecast(Point(0, 100));
        var weather = Rain(1.0);

        var input = projector.Project(new LocationForecast(Lucerne, Weather: weather, Flood: flood), null);

        Assert.NotNull(input);
        Assert.Multiple(
            () => Assert.Equal(2, projector.RequiredDomains.Count),
            () => Assert.Contains(DomainKind.Flood, projector.RequiredDomains),
            () => Assert.Contains(DomainKind.Weather, projector.RequiredDomains),
            () => Assert.Same(flood, input.Flood),
            () => Assert.Same(weather, input.Weather),
            () => Assert.Null(projector.Project(new LocationForecast(Lucerne, Flood: flood), null)),
            () => Assert.Null(projector.Project(new LocationForecast(Lucerne, Weather: weather), null)));
    }

    [Fact]
    public void The_registered_risk_group_raises_end_to_end_and_is_affected_by_flood_only()
    {
        var group = new ProjectionGroup<FloodRiskInput>(
            new FloodRiskProjector(), [new FloodRiskAlertEnrichment(new FloodRiskAlertRule(), AlertRuleFixture.Defaults, true)]);
        var current = new LocationForecast(Lucerne, Flood: FloodForecast(Point(0, 500)));

        var events = group.Evaluate(current, null, new FakeTimeProvider(Now));

        var raised = Assert.IsType<AlertRaisedEvent>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Red, raised.Alert.Severity),
            () => Assert.True(group.AffectedBy(DomainKind.Flood)),
            () => Assert.False(group.AffectedBy(DomainKind.Weather)));
    }
}
