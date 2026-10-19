using Microsoft.Extensions.Time.Testing;
using Njord.Alerts.AirQuality;
using Njord.Core.Domain;
using Njord.Messages.Alerts;
using Njord.Model;
using static Njord.Alerts.Tests.AirQuality.AirQualityFixture;

namespace Njord.Alerts.Tests.AirQuality;

public sealed class AirQualityAlertEnrichmentSpec
{
    private static readonly FakeTimeProvider Time = new(Now);

    private static AirQualityAlertEnrichment Create(bool enabled = true) =>
        new([new PollenAlertRule(), new Pm25AlertRule()], enabled);

    [Fact]
    public void Only_alerts_above_none_are_raised_with_the_location_and_time()
    {
        var input = new AirQualityAlertInput(Lucerne, Day((_, p) => p with { Pm25 = 20.0 }));

        var raised = Assert.IsType<AirQualityAlertRaisedEvent>(Assert.Single(Create().Evaluate(input, Time)));

        Assert.Multiple(
            () => Assert.Equal(Lucerne, raised.Location),
            () => Assert.Equal(Now, raised.Timestamp),
            () => Assert.Equal(AirQualityAlertType.Pm25, raised.Alert.Type),
            () => Assert.Equal(AlertSeverity.Yellow, raised.Alert.Severity));
    }

    [Fact]
    public void Both_rules_can_raise_in_one_evaluation()
    {
        var input = new AirQualityAlertInput(Lucerne, Day((_, p) => p with { Pm25 = 30.0, GrassPollen = 70.0 }));

        var types = Create().Evaluate(input, Time).Cast<AirQualityAlertRaisedEvent>().Select(e => e.Alert.Type).ToList();

        Assert.Equal([AirQualityAlertType.Pollen, AirQualityAlertType.Pm25], types);
    }

    [Fact]
    public void A_disabled_enrichment_raises_nothing()
    {
        var input = new AirQualityAlertInput(Lucerne, Day((_, p) => p with { Pm25 = 80.0 }));

        Assert.Empty(Create(enabled: false).Evaluate(input, Time));
    }
}
