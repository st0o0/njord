using Njord.Messages.Alerts;

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
