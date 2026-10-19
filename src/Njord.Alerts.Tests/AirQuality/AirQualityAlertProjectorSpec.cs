using Njord.Alerts.Tests.Rules;
using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;
using static Njord.Alerts.Tests.AirQuality.AirQualityFixture;

namespace Njord.Alerts.Tests.AirQuality;

public sealed class AirQualityAlertProjectorSpec
{
    private static readonly AirQualityAlertProjector Projector = new();

    [Fact]
    public void The_projector_requires_only_the_air_quality_domain()
    {
        Assert.Equal([DomainKind.AirQuality], Projector.RequiredDomains);
    }

    [Fact]
    public void A_location_forecast_without_air_quality_projects_nothing()
    {
        Assert.Null(Projector.Project(new LocationForecast(Lucerne), null));
    }

    [Fact]
    public void The_air_quality_forecast_is_projected_with_its_location()
    {
        var forecast = Day();

        var input = Projector.Project(new LocationForecast(Lucerne, AirQuality: forecast), null);

        Assert.NotNull(input);
        Assert.Multiple(
            () => Assert.Equal(Lucerne, input.Location),
            () => Assert.Same(forecast, input.AirQuality));
    }

    [Fact]
    public void The_registered_group_raises_events_end_to_end_from_a_location_forecast()
    {
        var group = new ProjectionGroup<AirQualityAlertInput>(
            Projector, [new AirQualityAlertEnrichment([new Pm25AlertRule()], AlertRuleFixture.Defaults, true)]);
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 60.0 }));

        var events = group.Evaluate(current, null, new FakeTimeProvider(Now));

        var raised = Assert.IsType<AlertRaisedEvent>(Assert.Single(events));
        Assert.Equal(AlertSeverity.Red, raised.Alert.Severity);
        Assert.True(group.AffectedBy(DomainKind.AirQuality));
        Assert.False(group.AffectedBy(DomainKind.Weather));
    }
}
