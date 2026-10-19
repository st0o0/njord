using Microsoft.Extensions.Time.Testing;
using Njord.AirQuality;
using Njord.Alerts;
using Njord.Messages.Alerts;
using Njord.Tests.Shared;
using static Njord.AirQuality.Tests.AirQualityFixture;

namespace Njord.AirQuality.Tests;

public sealed class AirQualityAlertEnrichmentSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    private static AirQualityAlertEnrichment Create(bool enabled = true) =>
        new([new PollenAlertRule(), new Pm25AlertRule()], AlertRuleFixture.Defaults, enabled);

    [Fact]
    public void Only_alerts_above_none_are_raised_with_the_location_and_time()
    {
        var input = new AirQualityAlertInput(Lucerne, Day((_, p) => p with { Pm25 = 20.0 }));

        var raised = Assert.IsType<AlertRaisedEvent>(Assert.Single(Create().Evaluate(input, Time)));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp),
            () => Assert.Equal(AlertType.Pm25, raised.Alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, raised.Alert.Severity));
    }

    [Fact]
    public void Both_rules_can_raise_in_one_evaluation()
    {
        var input = new AirQualityAlertInput(Lucerne, Day((_, p) => p with { Pm25 = 30.0, GrassPollen = 70.0 }));

        var types = Create().Evaluate(input, Time).Cast<AlertRaisedEvent>().Select(e => e.Alert.Type).ToList();

        Assert.Equal([AlertType.Pollen, AlertType.Pm25], types);
    }

    [Fact]
    public void A_disabled_enrichment_raises_nothing()
    {
        var input = new AirQualityAlertInput(Lucerne, Day((_, p) => p with { Pm25 = 80.0 }));

        Assert.Empty(Create(enabled: false).Evaluate(input, Time));
    }
}
