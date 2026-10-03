using Njord.Domain.Sensors;

namespace Njord.Messages.Sensors;

public sealed record UpdateReading(SensorReading Reading);

public sealed record QuerySensorSnapshot(string Location);

public abstract record QuerySensorSnapshotResponse;
public sealed record SensorSnapshotFound(SensorSnapshot Snapshot) : QuerySensorSnapshotResponse;
public sealed record SensorSnapshotNotFound(string Location) : QuerySensorSnapshotResponse;
public sealed record QuerySensorSnapshotFailed(Exception Cause) : QuerySensorSnapshotResponse;
