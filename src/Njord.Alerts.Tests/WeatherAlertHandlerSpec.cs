using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast;
using Njord.Forecast.Weather;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Alerts.Tests;

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

        var frost = Assert.Single(events.OfType<AlertResult>(), e => e.WireKey == "frost");
        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.Orange, frost.Alert.Severity),
            () => Assert.Equal(Lucerne, frost.Location),
            () => Assert.Equal(Now, frost.Timestamp));
    }

    [Fact]
    public void Handle_omits_alerts_without_severity()
    {
        var events = FeatureServices.WeatherHandler(Time).Handle(Freezing(), null, Time);

        Assert.All(events.OfType<AlertResult>(), e => Assert.NotEqual(AlertSeverity.None, e.Alert.Severity));
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

    private static IEnumerable<string> RaisedTypes(IReadOnlyList<PipelineEvent> events) =>
        events.OfType<AlertResult>().Select(e => e.WireKey);

    [Fact]
    public void Handle_runs_every_rule_when_all_features_are_enabled()
    {
        var types = RaisedTypes(FeatureServices.WeatherHandler(Time).Handle(FrostAndHeat(), null, Time)).ToList();

        Assert.Multiple(
            () => Assert.Contains("frost", types),
            () => Assert.Contains("heat", types));
    }

    [Fact]
    public void Handle_suppresses_frost_alerts_but_keeps_heat_alerts_when_frost_is_disabled()
    {
        var handler = FeatureServices.WeatherHandler(Time, features: f => f.Set("Weather", "Frost", false));

        var types = RaisedTypes(handler.Handle(FrostAndHeat(), null, Time)).ToList();

        Assert.Multiple(
            () => Assert.DoesNotContain("frost", types),
            () => Assert.Contains("heat", types));
    }

    [Fact]
    public void Handle_suppresses_heat_alerts_but_keeps_frost_alerts_when_heat_is_disabled()
    {
        var handler = FeatureServices.WeatherHandler(Time, features: f => f.Set("Weather", "Heat", false));

        var types = RaisedTypes(handler.Handle(FrostAndHeat(), null, Time)).ToList();

        Assert.Multiple(
            () => Assert.Contains("frost", types),
            () => Assert.DoesNotContain("heat", types));
    }

    [Fact]
    public void Feature_unites_the_requirements_of_the_enabled_rules_only()
    {
        var handler = FeatureServices.WeatherHandler(Time, features: f =>
        {
            foreach (var rule in FeatureServices.WeatherHandler(Time).Features)
            {
                f.Set("Weather", rule.Name, rule.Name is "Frost" or "Storm");
            }
        });

        Assert.Equal(
            WeatherParams.Temperature | WeatherParams.WindGusts | WeatherParams.EnsembleStdDev,
            handler.Feature.Requirements.Weather);
    }

    [Fact]
    public void Features_list_every_rule_feature()
    {
        var names = FeatureServices.WeatherHandler(Time).Features.Select(f => f.Name).ToList();

        Assert.Equal(14, names.Count);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
