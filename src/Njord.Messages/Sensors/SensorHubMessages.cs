using Njord.Sensors;

namespace Njord.Messages.Sensors;

public sealed record UpdateReading(SensorReading Reading);

public sealed record QuerySensorSnapshot(string Location);

public abstract record QuerySensorSnapshotResponse;
public sealed record QuerySensorSnapshotCompleted(SensorSnapshot Snapshot) : QuerySensorSnapshotResponse;
public sealed record QuerySensorSnapshotNotAvailable(string Location) : QuerySensorSnapshotResponse;

