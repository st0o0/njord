using Njord.Compute.Consensus;
using Njord.Compute.Contracts;
using Njord.Grpc.Mapping;
using Njord.Messages.Pipeline;
using Njord.Tests.Shared;
using Njord.Weather;
using DomainAlert = Njord.Compute.Contracts.Alert;
using DomainAlertSeverity = Njord.Compute.Contracts.AlertSeverity;
using DomainAlertType = Njord.Compute.Contracts.AlertType;
using DomainHorizonConsensus = Njord.Compute.Consensus.HorizonConsensus;
using DomainHorizonDerived = Njord.Compute.Contracts.HorizonDerived;
using DomainParameterConsensus = Njord.Compute.Consensus.ParameterConsensus;
using DomainParameterTrend = Njord.Compute.Contracts.ParameterTrend;
using DomainScalarDerived = Njord.Compute.Contracts.ScalarDerived;

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
