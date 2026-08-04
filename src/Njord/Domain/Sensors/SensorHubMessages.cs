namespace Njord.Domain.Sensors;

public sealed record UpdateReading(SensorReading Reading);

public sealed record GetSnapshot(string Location);

public sealed record SensorSnapshotResponse(SensorSnapshot? Snapshot);
