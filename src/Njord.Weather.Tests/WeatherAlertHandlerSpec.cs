using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Forecast;
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
}
