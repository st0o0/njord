using Njord.Sensors;

namespace Njord.Messages.Sensors;

public sealed record QuerySensorSnapshot(string Location);

public abstract record SensorSnapshotResponse;
public sealed record SensorSnapshotResult(SensorSnapshot Snapshot) : SensorSnapshotResponse;
public sealed record SensorSnapshotNotAvailable(string Location) : SensorSnapshotResponse;
