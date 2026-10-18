using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Model;
using DomainAlert = Njord.Messages.Alerts.Alert;
using DomainAlertSeverity = Njord.Messages.Alerts.AlertSeverity;
using DomainAlertType = Njord.Messages.Alerts.AlertType;

namespace Njord.Grpc.Tests;

public sealed class AlertProtoMappingsSpec
{
    [Fact]
    public void Alerts_should_map_alert_type_and_severity_enums()
    {
        var result = new AlertResult(new LocationId("lucerne"),
        [
            new DomainAlert(DomainAlertType.Frost, DomainAlertSeverity.Orange, 0.85,
                NoDetails.Instance),
            new DomainAlert(DomainAlertType.Storm, DomainAlertSeverity.Red, 0.92,
                NoDetails.Instance),
        ]);

        var update = AlertProtoMappings.ToProto(result);

        Assert.Equal(2, update.Alerts.Count);

        Assert.Equal(V2.AlertType.Frost, update.Alerts[0].Type);
        Assert.Equal(V2.AlertSeverity.Orange, update.Alerts[0].Severity);
        Assert.Equal(0.85, update.Alerts[0].Confidence);

        Assert.Equal(V2.AlertType.Storm, update.Alerts[1].Type);
        Assert.Equal(V2.AlertSeverity.Red, update.Alerts[1].Severity);
        Assert.Equal(0.92, update.Alerts[1].Confidence);
    }
}
