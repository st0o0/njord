namespace Njord.Domain.Sensors;

public sealed record SensorReading(
    SensorKind Kind,
    string Location,
    string Source,
    double Value,
    DateTimeOffset MeasuredAt);
