using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;
using static Njord.AirQuality.Tests.AirQualityFixture;

namespace Njord.AirQuality.Tests;

public sealed class AirQualityAlertHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    private static AirQualityAlertHandler Create(bool enabled = true) =>
        new([new PollenAlertRule(), new Pm25AlertRule()], AlertRuleFixture.Defaults, enabled);

    [Fact]
    public void Triggers_only_on_the_air_quality_domain()
    {
        Assert.Equal([DomainKind.AirQuality], Create().Triggers);
    }

    [Fact]
    public void Handle_returns_nothing_without_air_quality()
    {
        Assert.Empty(Create().Handle(new LocationForecast(Lucerne), null, Time));
    }

    [Fact]
    public void Handle_raises_a_pm25_alert_with_location_and_timestamp()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 25.0 }));

        var raised = Assert.IsType<PipelineEvent.AlertRaised>(Assert.Single(Create().Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal(AlertType.Pm25, raised.Type),
            () => Assert.Equal(AlertSeverity.Orange, raised.Severity),
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp));
    }

    [Fact]
    public void Handle_raises_nothing_for_clean_air()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day());

        Assert.Empty(Create().Handle(current, null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_when_alerts_are_disabled()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 80.0 }));

        Assert.Empty(Create(enabled: false).Handle(current, null, Time));
    }
}
