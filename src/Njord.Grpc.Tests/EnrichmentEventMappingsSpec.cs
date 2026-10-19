using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Pipeline;
using Njord.Model;
using Njord.Tests.Shared;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;

namespace Njord.Grpc.Tests;

public sealed class EnrichmentEventMappingsSpec
{
    [Fact]
    public void ToEnrichmentEvent_should_wrap_alert_result_in_enrichment_event()
    {
        var alertResult = new AlertResult(new Location("lucerne", 0, 0),
        [
            new DomainAlert(DomainAlertType.Heat, DomainAlertSeverity.Yellow, 0.75, NoDetails.Instance),
        ]);
        var updatedAt = TestTimeConstants.BudgetEpoch;

        var evt = EnrichmentEventMappings.ToEnrichmentEvent(new PipelineEvent.AlertComputed(alertResult, updatedAt));

        Assert.NotNull(evt);
        Assert.Equal("lucerne", evt.Location);
        Assert.Equal("alerts", evt.TypeName);
        Assert.Equal(updatedAt, evt.UpdatedAt.ToDateTimeOffset());
        var alert = Assert.Single(evt.Alerts.Alerts);
        Assert.Equal((V2.AlertType.Heat, V2.AlertSeverity.Yellow, 0.75), (alert.Type, alert.Severity, alert.Confidence));
    }

    [Fact]
    public void ToEnrichmentEvent_should_wrap_a_raised_alert_in_enrichment_event()
    {
        var raisedAt = TestTimeConstants.BudgetEpoch;
        var raised = new PipelineEvent.AlertRaised(DomainData.Lucerne,
            new DomainAlert(DomainAlertType.Frost, DomainAlertSeverity.Orange, 0.9, NoDetails.Instance, PeakValue: -4.0, HoursUntil: 6),
            raisedAt);

        var evt = EnrichmentEventMappings.ToEnrichmentEvent(raised);

        Assert.NotNull(evt);
        Assert.Equal(("Lucerne", "alerts", raisedAt), (evt.Location, evt.TypeName, evt.UpdatedAt.ToDateTimeOffset()));
        var alert = Assert.Single(evt.Alerts.Alerts);
        Assert.Equal((V2.AlertType.Frost, V2.AlertSeverity.Orange, -4.0, 6), (alert.Type, alert.Severity, alert.PeakValue, alert.HoursUntil));
    }

    [Fact]
    public void ToEnrichmentEvent_should_wrap_a_raised_air_quality_alert_in_enrichment_event()
    {
        var raisedAt = TestTimeConstants.BudgetEpoch;
        var raised = new PipelineEvent.AirQualityAlertRaised(DomainData.Lucerne,
            new AirQualityAlert(AirQualityAlertType.Pollen, DomainAlertSeverity.Orange, 85.0, 50.0, HoursUntil: 3),
            raisedAt);

        var evt = EnrichmentEventMappings.ToEnrichmentEvent(raised);

        Assert.NotNull(evt);
        Assert.Equal(("Lucerne", "alerts", raisedAt), (evt.Location, evt.TypeName, evt.UpdatedAt.ToDateTimeOffset()));
        var alert = Assert.Single(evt.Alerts.Alerts);
        Assert.Equal(
            (V2.AlertType.Pollen, V2.AlertSeverity.Orange, 1.0, 85.0, 50.0, 3),
            (alert.Type, alert.Severity, alert.Confidence, alert.TriggerValue, alert.Threshold, alert.HoursUntil));
    }

    [Fact]
    public void ToEnrichmentEvent_should_wrap_a_raised_flood_alert_in_enrichment_event()
    {
        var raisedAt = TestTimeConstants.BudgetEpoch;
        var raised = new PipelineEvent.FloodAlertRaised(DomainData.Lucerne,
            new FloodAlert(FloodAlertType.FloodRisk, DomainAlertSeverity.Red, 120.0, 100.0),
            raisedAt);

        var evt = EnrichmentEventMappings.ToEnrichmentEvent(raised);

        Assert.NotNull(evt);
        Assert.Equal(("Lucerne", "alerts", raisedAt), (evt.Location, evt.TypeName, evt.UpdatedAt.ToDateTimeOffset()));
        var alert = Assert.Single(evt.Alerts.Alerts);
        Assert.Equal(
            (V2.AlertType.FloodRisk, V2.AlertSeverity.Red, 120.0, 100.0, false),
            (alert.Type, alert.Severity, alert.TriggerValue, alert.Threshold, alert.HasHoursUntil));
    }

    [Fact]
    public void ToEnrichmentEvent_should_return_null_for_domain_data_events()
    {
        var evt = EnrichmentEventMappings.ToEnrichmentEvent(
            new PipelineEvent.WeatherDataReceived(DomainData.Lucerne, DomainData.Weather(TestTimeConstants.BudgetEpoch, 1)));

        Assert.Null(evt);
    }

    [Fact]
    public void LatestAnalysisResult_should_fill_the_alerts()
    {
        var lucerne = new Location("lucerne", 0, 0);
        var result = new LatestAnalysisResult(lucerne, EnrichmentEvents.Alerts(lucerne, TestTimeConstants.BudgetEpoch));

        var proto = EnrichmentEventMappings.ToProto((LatestAnalysisResponse)result);

        Assert.Equal(new V2.GetEnrichmentsResponse { Location = "lucerne", Alerts = new V2.AlertUpdate() }, proto);
    }

    [Fact]
    public void LatestAnalysisResult_should_leave_missing_alerts_unset()
    {
        var proto = EnrichmentEventMappings.ToProto(new LatestAnalysisResult(new Location("lucerne", 0, 0), null));

        Assert.Multiple(
            () => Assert.Equal("lucerne", proto.Location),
            () => Assert.Null(proto.Alerts));
    }

    [Fact]
    public void LatestAnalysisNotAvailable_should_carry_only_the_location()
    {
        var proto = EnrichmentEventMappings.ToProto(new LatestAnalysisNotAvailable(new Location("lucerne", 0, 0)));

        Assert.Equal(new V2.GetEnrichmentsResponse { Location = "lucerne" }, proto);
    }
}
