using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;

namespace Njord.Messages.Tests.Alerts;

public sealed class AlertSpec
{
    [Fact]
    public void Should_create_a_none_alert_without_details_or_confidence()
    {
        var alert = Alert.None(AlertType.Frost);

        Assert.Multiple(
            () => Assert.Equal(AlertType.Frost, alert.Type),
            () => Assert.Equal(AlertSeverity.None, alert.Severity),
            () => Assert.Equal(0.0, alert.Confidence),
            () => Assert.Same(NoDetails.Instance, alert.Details));
    }

    [Fact]
    public void Should_expose_alert_fields_on_the_raised_pipeline_event()
    {
        var details = new FrostDetails(-4, Horizon.Hours(6), 3);
        var alert = new Alert(AlertType.Frost, AlertSeverity.Orange, 0.9, details, Level: ConfidenceLevel.High);
        var raised = new PipelineEvent.AlertRaised(
            new Location("Lucerne", 47, 8), alert, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Multiple(
            () => Assert.Equal(AlertType.Frost, raised.Type),
            () => Assert.Equal(AlertSeverity.Orange, raised.Severity),
            () => Assert.Equal(ConfidenceLevel.High, raised.Confidence),
            () => Assert.Same(details, raised.Details));
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
