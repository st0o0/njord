using Njord.Forecast;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;

namespace Njord.Messages.Tests.Alerts;

public sealed class AlertSpec
{
    [Fact]
    public void Should_create_a_none_alert_without_details_or_confidence()
    {
        var alert = Alert.None();

        Assert.Multiple(
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.Confidence),
            () => Assert.Same(NoDetails.Instance, alert.Details));
    }

    [Fact]
    public void Should_expose_the_alert_on_the_alert_result()
    {
        var details = new FrostDetails(-4, Horizon.Hours(6), 3);
        var alert = new Alert(AlertSeverity.Orange, 0.9, details, Level: ConfidenceLevel.High);
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = new AlertResult(new Location("Lucerne", 47, 8), "frost", alert, timestamp);

        Assert.Multiple(
            () => Assert.Equal("frost", result.WireKey),
            () => Assert.Equal(OutputKind.Alert, result.Output),
            () => Assert.Same(alert, result.Alert),
            () => Assert.Equal(timestamp, result.Timestamp));
    }

    [Theory]
    [InlineData(null, ConfidenceLevel.Medium)]
    [InlineData(0.5, ConfidenceLevel.High)]
    [InlineData(2.0, ConfidenceLevel.Medium)]
    [InlineData(4.0, ConfidenceLevel.Low)]
    public void Should_derive_confidence_level_from_ensemble_spread(double? stdDev, ConfidenceLevel expected)
    {
        Assert.Equal(expected, ConfidenceThresholds.Default.LevelFor(stdDev));
    }
}
