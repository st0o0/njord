using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast.AirQuality;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AirQualityFixture;

namespace Njord.Alerts.Tests;

public sealed class AirQualityAlertHandlerSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    [Fact]
    public void Handle_raises_an_alert_with_location_and_timestamp_when_air_quality_is_poor()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 25.0 }));

        var raised = Assert.IsType<AlertResult>(Assert.Single(
            FeatureServices.AirQualityHandler(Time).Handle(current, null, Time)));

        Assert.Multiple(
            () => Assert.Equal("pm25", raised.WireKey),
            () => Assert.Equal(AlertSeverity.Orange, raised.Alert.Severity),
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp));
    }

    [Fact]
    public void Handle_raises_nothing_for_clean_air()
    {
        Assert.Empty(FeatureServices.AirQualityHandler(Time).Handle(
            new LocationForecast(Lucerne, AirQuality: Day()), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_air_quality_data()
    {
        Assert.Empty(FeatureServices.AirQualityHandler(Time).Handle(
            new LocationForecast(Lucerne), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_when_alerts_are_disabled()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with { Pm25 = 80.0 }));

        Assert.Empty(FeatureServices.AirQualityHandler(Time, enabled: false).Handle(current, null, Time));
    }

    [Fact]
    public void Handle_runs_every_enabled_rule()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with
        {
            Pm25 = 25.0,
            BirchPollen = 90.0,
            Dust = 200.0,
        }));

        var handler = FeatureServices.AirQualityHandler(Time, features: f =>
        {
            f.Set("AirQuality", "Pollen", true);
            f.Set("AirQuality", "Pm25", true);
            f.Set("AirQuality", "Dust", true);
        });

        var wireKeys = handler.Handle(current, null, Time)
            .OfType<AlertResult>()
            .Select(e => e.WireKey)
            .ToList();

        Assert.Multiple(
            () => Assert.Contains("pollen", wireKeys),
            () => Assert.Contains("pm25", wireKeys),
            () => Assert.Contains("dust", wireKeys));
    }

    [Fact]
    public void Handle_suppresses_pollen_alerts_but_keeps_pm25_when_pollen_is_disabled()
    {
        var current = new LocationForecast(Lucerne, AirQuality: Day((_, p) => p with
        {
            Pm25 = 25.0,
            BirchPollen = 90.0,
        }));

        var handler = FeatureServices.AirQualityHandler(Time, features: f =>
            f.Set("AirQuality", "Pollen", false));

        var wireKeys = handler.Handle(current, null, Time)
            .OfType<AlertResult>()
            .Select(e => e.WireKey)
            .ToList();

        Assert.Multiple(
            () => Assert.DoesNotContain("pollen", wireKeys),
            () => Assert.Contains("pm25", wireKeys));
    }

    [Fact]
    public void Features_list_every_rule_feature()
    {
        var names = FeatureServices.AirQualityHandler(Time).Features.Select(f => f.Name).ToList();

        Assert.Equal(3, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Feature_unites_the_requirements_of_the_enabled_rules_only()
    {
        var handler = FeatureServices.AirQualityHandler(Time, features: f =>
        {
            f.Set("AirQuality", "Pollen", true);
            f.Set("AirQuality", "Pm25", false);
            f.Set("AirQuality", "Dust", false);
        });

        Assert.Equal(
            AirQualityParams.BirchPollen | AirQualityParams.GrassPollen
                | AirQualityParams.Alder | AirQualityParams.Mugwort,
            handler.Feature.Requirements.AirQuality);
    }

    [Fact]
    public void Name_is_AirQualityAlerts()
    {
        Assert.Equal("AirQualityAlerts", FeatureServices.AirQualityHandler(Time).Name);
    }
}
