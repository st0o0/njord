using System.Collections.Immutable;
using Njord.Model;
using Njord.Sensors;

namespace Njord.Messages.Sensors;

public sealed record LocationSensorReadings(ImmutableDictionary<LocationId, SensorReadings> ByLocation)
{
    public static readonly LocationSensorReadings Empty = new(ImmutableDictionary<LocationId, SensorReadings>.Empty);

    public SensorReadings? For(LocationId location) => ByLocation.GetValueOrDefault(location);
}
