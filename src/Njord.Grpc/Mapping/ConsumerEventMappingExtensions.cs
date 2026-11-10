using Google.Protobuf.WellKnownTypes;

namespace Njord.Grpc.Mapping;

internal static class ConsumerEventMappingExtensions
{
    public static Proto.EntityEvent ToProto(this ConsumerEvent evt) => evt switch
    {
        ConsumerSnapshot snap => new Proto.EntityEvent { Snapshot = snap.ToProtoSnapshot() },
        EntityStateChanged changed => new Proto.EntityEvent
        {
            StateUpdate = changed.Result.ToEntityStateUpdate(changed.EntityId, changed.UpdatedAt),
        },
        EntityBecameUnavailable u => new Proto.EntityEvent
        {
            Unavailable = new Proto.EntityUnavailable
            {
                EntityId = u.EntityId,
                Since = Timestamp.FromDateTimeOffset(u.Since),
            },
        },
        EntityBecameAvailable a => new Proto.EntityEvent
        {
            Available = new Proto.EntityAvailable
            {
                EntityId = a.EntityId,
                RecoveredAt = Timestamp.FromDateTimeOffset(a.RecoveredAt),
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(evt), evt.GetType().Name, null),
    };

    private static Proto.EntitySnapshot ToProtoSnapshot(this ConsumerSnapshot snap)
    {
        var proto = new Proto.EntitySnapshot();
        proto.States.AddRange(snap.States
            .Select(e => e.Result.ToEntityStateUpdate(e.EntityId, e.LastUpdated)));
        return proto;
    }
}
