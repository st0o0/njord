using Microsoft.Extensions.Time.Testing;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;
using Njord.Weather.Alerts;
using static Njord.Tests.Shared.AlertRuleFixture;

namespace Njord.Weather.Tests;

public sealed class WeatherAlertHandlerSpec
{
    private static readonly Location Lucerne = new("lucerne", 47.05, 8.31);
    private static readonly FakeTimeProvider Time = new(Now);

    private static WeatherAlertHandler Create(bool enabled = true) =>
        new(FeatureServices.Alerts(Time, enabled), Defaults);

    [Fact]
    public void Triggers_only_on_the_weather_domain()
    {
        Assert.Equal([DomainKind.Weather], Create().Triggers);
    }

    [Fact]
    public void Handle_returns_nothing_without_weather()
    {
        Assert.Empty(Create().Handle(new LocationForecast(Lucerne), null, Time));
    }

    [Fact]
    public void Handle_raises_only_alerts_with_a_severity()
    {
        var current = new LocationForecast(Lucerne, Day((_, p) => p with { Temperature = -6.0 }));

        var events = Create().Handle(current, null, Time);

        var raised = events.Cast<PipelineEvent.AlertRaised>().ToList();
        var frost = Assert.Single(raised, e => e.Type == AlertType.Frost);
        Assert.Multiple(
            () => Assert.NotEmpty(raised),
            () => Assert.All(raised, e => Assert.NotEqual(AlertSeverity.None, e.Severity)),
            () => Assert.Equal(AlertSeverity.Orange, frost.Severity),
            () => Assert.Equal(Lucerne, frost.Location),
            () => Assert.Equal(Now, frost.Timestamp));
    }

    [Fact]
    public void Handle_returns_nothing_when_alerts_are_disabled()
    {
        var current = new LocationForecast(Lucerne, Day((_, p) => p with { Temperature = -6.0 }));

        Assert.Empty(Create(enabled: false).Handle(current, null, Time));
    }
}
