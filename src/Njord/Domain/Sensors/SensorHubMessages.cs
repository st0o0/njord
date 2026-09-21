namespace Njord.Domain.Sensors;

public sealed record UpdateReading(SensorReading Reading);

public sealed record QuerySensorSnapshot(string Location);

public abstract record SensorSnapshotQueryResponse;
public sealed record SensorSnapshotFound(SensorSnapshot Snapshot) : SensorSnapshotQueryResponse;
public sealed record SensorSnapshotNotFound(string Location) : SensorSnapshotQueryResponse;
public sealed record SensorSnapshotQueryFailed(Exception Cause) : SensorSnapshotQueryResponse;
