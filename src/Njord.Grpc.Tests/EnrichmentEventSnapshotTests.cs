using Google.Protobuf;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests;

public sealed class EnrichmentEventSnapshotTests
{
    private static readonly DateTimeOffset Anchor = TestTimeConstants.BudgetEpoch;

    [Fact]
    public Task A_frost_alert_event_keeps_its_wire_shape()
    {
        var result = new FrostAlertResult(DomainData.Lucerne, AlertSeverity.Orange, 0.85, -3.5, -2.0, -4.0, 6, 5,
            ConfidenceLevel.High, -4.0, 6);

        var evt = result.ToEnrichmentEvent(Anchor);

        Assert.NotNull(evt);
        return Verify(JsonFormatter.Default.Format(evt));
    }
}
