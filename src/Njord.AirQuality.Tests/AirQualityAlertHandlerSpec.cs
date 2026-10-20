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

    private static AirQualityAlertHandler Create(bool enabled = true, AlertType type = AlertType.Pm25) =>
        new([new PollenAlertRule(), new Pm25AlertRule()], AlertRuleFixture.OptionsWith(enabled), type);

    [Fact]
    public void Handle_raises_an_alert_with_location_and_timestamp_when_air_quality_is_poor()
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
        Assert.Empty(Create().Handle(new LocationForecast(Lucerne, AirQuality: Day()), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_air_quality_data()
    {
        Assert.Empty(Create().Handle(new LocationForecast(Lucerne), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_when_alerts_are_disabled()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 80.0 }));

        Assert.Empty(Create(enabled: false).Handle(current, null, Time));
    }

    [Theory]
    [InlineData(AlertType.Pollen, "PollenAlert")]
    [InlineData(AlertType.Pm25, "Pm25Alert")]
    public void ConfigKey_names_the_enrichment_of_the_handled_alert_type(AlertType type, string expected)
    {
        Assert.Equal(expected, Create(type: type).ConfigKey);
    }

    [Fact]
    public void Handle_evaluates_only_the_rules_of_its_alert_type()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 25.0 }));

        Assert.Empty(Create(type: AlertType.Pollen).Handle(current, null, Time));
    }
}
