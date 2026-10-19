using Microsoft.Extensions.Time.Testing;
using Njord.Messages.Alerts;
using Njord.Model;
using Njord.Tests.Shared;
using static Njord.Alerts.Tests.Rules.AlertRuleFixture;

namespace Njord.Alerts.Tests;

public sealed class WeatherAlertEnrichmentSpec
{
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);

    [Fact]
    public void Evaluate_emits_events_only_for_active_alerts()
    {
        var time = new FakeTimeProvider(Now);
        var enrichment = new WeatherAlertEnrichment(FeatureServices.Alerts(time), Defaults);
        var input = new WeatherAlertInput(Lucerne, Uniform(p => p with { Temperature = -8.0 }), null);

        var events = enrichment.Evaluate(input, time);

        var raised = Assert.IsType<AlertRaisedEvent>(Assert.Single(events));
        Assert.Multiple(
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(AlertType.Frost, raised.Alert.Type),
            () => Assert.Equal(AlertSeverity.Orange, raised.Alert.Severity),
            () => Assert.Equal(-8.0, raised.Alert.TriggerValue));
    }

    [Fact]
    public void Evaluate_stamps_events_with_the_time_provider()
    {
        var time = new FakeTimeProvider(Now);
        var enrichment = new WeatherAlertEnrichment(FeatureServices.Alerts(time), Defaults);
        var input = new WeatherAlertInput(Lucerne, Uniform(p => p with { Temperature = -8.0 }), null);
        time.Advance(TimeSpan.FromMinutes(20));

        var events = enrichment.Evaluate(input, time);

        var raised = Assert.IsType<AlertRaisedEvent>(Assert.Single(events));
        Assert.Equal(Now.AddMinutes(20), raised.Timestamp);
    }

    [Fact]
    public void Evaluate_emits_one_event_per_active_alert_type()
    {
        var time = new FakeTimeProvider(Now);
        var enrichment = new WeatherAlertEnrichment(FeatureServices.Alerts(time), Defaults);
        var input = new WeatherAlertInput(
            Lucerne, Uniform(p => p with { Temperature = -8.0, ApparentTemperature = -12.0 }), null);

        var events = enrichment.Evaluate(input, time);

        var types = events.Cast<AlertRaisedEvent>().Select(e => e.Alert.Type);
        Assert.Equal([AlertType.Frost, AlertType.WindChill], types);
    }

    [Fact]
    public void Evaluate_emits_nothing_for_benign_weather()
    {
        var time = new FakeTimeProvider(Now);
        var enrichment = new WeatherAlertEnrichment(FeatureServices.Alerts(time), Defaults);

        var events = enrichment.Evaluate(new WeatherAlertInput(Lucerne, Day(), null), time);

        Assert.Empty(events);
    }

    [Fact]
    public void Evaluate_emits_nothing_when_alerts_are_disabled()
    {
        var time = new FakeTimeProvider(Now);
        var enrichment = new WeatherAlertEnrichment(FeatureServices.Alerts(time, enabled: false), Defaults);
        var input = new WeatherAlertInput(Lucerne, Uniform(p => p with { Temperature = -8.0 }), null);

        var events = enrichment.Evaluate(input, time);

        Assert.Empty(events);
    }
}
