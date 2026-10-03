namespace Njord.Domain.Sensors;

public sealed record AggregatedReading(double Value, int SourceCount, DateTimeOffset NewestMeasuredAt);

public sealed record SensorSnapshot(
    string Location,
    IReadOnlyDictionary<SensorKind, AggregatedReading> Readings)
{
    public double? Get(SensorKind kind) =>
        Readings.TryGetValue(kind, out var reading) ? reading.Value : null;
}
