using Njord.Sensors;
using Njord.Weather;

namespace Njord.Messages.Sensors;

public sealed record QuerySensorSnapshot(LocationId Location);

public abstract record SensorSnapshotResponse;
public sealed record SensorSnapshotResult(SensorSnapshot Snapshot) : SensorSnapshotResponse;
public sealed record SensorSnapshotNotAvailable(LocationId Location) : SensorSnapshotResponse;
