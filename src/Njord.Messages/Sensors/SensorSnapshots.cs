using System.Collections.Immutable;
using Njord.Sensors;
using Njord.Weather;

namespace Njord.Messages.Sensors;

public sealed record SensorSnapshots(ImmutableDictionary<LocationId, SensorSnapshot> Snapshots)
{
    public static readonly SensorSnapshots Empty = new(ImmutableDictionary<LocationId, SensorSnapshot>.Empty);

    public SensorSnapshot? For(LocationId location) => Snapshots.GetValueOrDefault(location);
}
