using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests;

public sealed class WeatherAlertHandlerSpec
{
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    private static readonly FakeTimeProvider Time = new(Now);

    private static LocationForecast Freezing() =>
        new(Lucerne, Uniform(p => p with { Temperature = -6.0 }));

    [Fact]
    public void Handle_raises_an_alert_for_each_rule_that_triggers()
    {
        var events = FeatureServices.WeatherHandler(Time).Handle(Freezing(), null, Time);

        var frost = Assert.Single(events.OfType<PipelineEvent.AlertRaised>(), e => e.Type == AlertType.Frost);
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, frost.Severity),
            () => Assert.Equal(Lucerne, frost.Location),
            () => Assert.Equal(Now, frost.Timestamp));
    }

    [Fact]
    public void Handle_omits_alerts_without_severity()
    {
        var events = FeatureServices.WeatherHandler(Time).Handle(Freezing(), null, Time);

        Assert.All(events.OfType<PipelineEvent.AlertRaised>(), e => Assert.NotEqual(AlertSeverity.None, e.Severity));
    }

    [Fact]
    public void Handle_returns_nothing_when_alerts_are_disabled()
    {
        Assert.Empty(FeatureServices.WeatherHandler(Time, enabled: false).Handle(Freezing(), null, Time));
    }

    [Fact]
    public void Handle_returns_nothing_without_weather_data()
    {
        Assert.Empty(FeatureServices.WeatherHandler(Time).Handle(new LocationForecast(Lucerne), null, Time));
    }

    private static LocationForecast FrostAndHeat() =>
        new(Lucerne, DomainData.Weather(Now, 48, (hour, point) => hour switch
        {
            2 => point with { Temperature = -6.0, ApparentTemperature = -6.0 },
            3 => point with { Temperature = 40.0, ApparentTemperature = 40.0 },
            _ => point,
        }));

    private static IEnumerable<AlertType> RaisedTypes(IReadOnlyList<PipelineEvent> events) =>
        events.OfType<PipelineEvent.AlertRaised>().Select(e => e.Type);

    [Fact]
    public void Handle_runs_every_rule_when_all_features_are_enabled()
    {
        var types = RaisedTypes(FeatureServices.WeatherHandler(Time).Handle(FrostAndHeat(), null, Time)).ToList();

        Assert.Multiple(
            () => Assert.Contains(AlertType.Frost, types),
            () => Assert.Contains(AlertType.Heat, types));
    }

    [Fact]
    public void Handle_suppresses_frost_alerts_but_keeps_heat_alerts_when_frost_is_disabled()
    {
        var handler = FeatureServices.WeatherHandler(Time, features: f => f[FeatureKind.FrostAlert] = false);

        var types = RaisedTypes(handler.Handle(FrostAndHeat(), null, Time)).ToList();

        Assert.Multiple(
            () => Assert.DoesNotContain(AlertType.Frost, types),
            () => Assert.Contains(AlertType.Heat, types));
    }

    [Fact]
    public void Handle_suppresses_heat_alerts_but_keeps_frost_alerts_when_heat_is_disabled()
    {
        var handler = FeatureServices.WeatherHandler(Time, features: f => f[FeatureKind.HeatAlert] = false);

        var types = RaisedTypes(handler.Handle(FrostAndHeat(), null, Time)).ToList();

        Assert.Multiple(
            () => Assert.Contains(AlertType.Frost, types),
            () => Assert.DoesNotContain(AlertType.Heat, types));
    }

    [Fact]
    public void Feature_unites_the_requirements_of_the_enabled_rules_only()
    {
        var handler = FeatureServices.WeatherHandler(Time, features: f =>
        {
            foreach (var kind in Enum.GetValues<FeatureKind>())
            {
                f[kind] = kind is FeatureKind.FrostAlert or FeatureKind.StormAlert;
            }
        });

        Assert.Equal(
            WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.EnsembleStdDev,
            handler.Feature.Requirements.Weather);
    }

    [Fact]
    public void Features_list_every_rule_feature()
    {
        var kinds = FeatureServices.WeatherHandler(Time).Features.Select(f => f.Kind).ToList();

        Assert.Equal(14, kinds.Count);
        Assert.Equal(kinds.Count, kinds.Distinct().Count());
    }
}
