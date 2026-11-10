using Njord.Core;
using Njord.Grpc.Mapping;
using Njord.Messages.Alerts;
using Njord.Messages.Results.Alerts;
using Njord.Tests.Shared;

namespace Njord.Grpc.Tests.Mapping;

public sealed class ConsumerEventMappingTests
{
    private static readonly Location _vienna = new("Vienna", 48.2, 16.37);
    private static readonly DateTimeOffset _now = TestTimeConstants.BudgetEpoch;

    [Fact]
    public void Snapshot_maps_to_proto_snapshot_with_all_states()
    {
        var alert = EnrichmentEvents.Alerts(_vienna, _now);
        var state = new EntityState("vienna_heat", alert, _now);
        var snap = new ConsumerSnapshot([state]);

        var proto = snap.ToProto();

        Assert.Equal(Proto.EntityEvent.EventOneofCase.Snapshot, proto.EventCase);
        var protoState = Assert.Single(proto.Snapshot.States);
        Assert.Equal("vienna_heat", protoState.EntityId);
    }

    [Fact]
    public void State_changed_maps_to_proto_state_update()
    {
        var alert = EnrichmentEvents.Alerts(_vienna, _now);
        var changed = new EntityStateChanged("vienna_heat", alert, _now);

        var proto = changed.ToProto();

        Assert.Multiple(
            () => Assert.Equal(Proto.EntityEvent.EventOneofCase.StateUpdate, proto.EventCase),
            () => Assert.Equal("vienna_heat", proto.StateUpdate.EntityId),
            () => Assert.Equal(Proto.EntityStateUpdate.ValueOneofCase.Alert, proto.StateUpdate.ValueCase));
    }

    [Fact]
    public void Unavailable_maps_to_proto_unavailable()
    {
        var evt = new EntityBecameUnavailable("vienna_heat", _now);

        var proto = evt.ToProto();

        Assert.Multiple(
            () => Assert.Equal(Proto.EntityEvent.EventOneofCase.Unavailable, proto.EventCase),
            () => Assert.Equal("vienna_heat", proto.Unavailable.EntityId));
    }

    [Fact]
    public void Available_maps_to_proto_available()
    {
        var evt = new EntityBecameAvailable("vienna_heat", _now);

        var proto = evt.ToProto();

        Assert.Multiple(
            () => Assert.Equal(Proto.EntityEvent.EventOneofCase.Available, proto.EventCase),
            () => Assert.Equal("vienna_heat", proto.Available.EntityId));
    }
}
