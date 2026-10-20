using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.AirQuality.Tests.AirQualityFixture;

namespace Njord.AirQuality.Tests;

public sealed class AirQualityAlertHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    private static AirQualityAlertHandler Create(bool enabled = true, string name = "Pm25") =>
        new([new PollenAlertRule(), new Pm25AlertRule()], AlertRuleFixture.OptionsWith(enabled), name);

    [Fact]
    public void Handle_raises_an_alert_with_location_and_timestamp_when_air_quality_is_poor()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 25.0 }));

        var raised = Assert.IsType<AlertResult>(Assert.Single(Create().Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal("pm25", raised.WireKey),
            () => Assert.Equal(AlertSeverity.Orange, raised.Alert.Severity),
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
    [InlineData("Pollen", "AirQuality:Pollen", "pollen")]
    [InlineData("Pm25", "AirQuality:Pm25", "pm25")]
    public void Feature_names_the_feature_of_the_handled_alert_rule(string name, string sectionKey, string wireKey)
    {
        var handler = Create(name: name);

        Assert.Multiple(
            () => Assert.Equal(name, handler.Name),
            () => Assert.Equal(sectionKey, handler.SectionKey()),
            () => Assert.Equal(wireKey, handler.WireKey()),
            () => Assert.Equal(OutputKind.Alert, handler.Feature.Output));
    }

    [Fact]
    public void Handle_evaluates_only_the_rules_of_its_alert_type()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 25.0 }));

        Assert.Empty(Create(name: "Pollen").Handle(current, null, Time));
    }
}
