namespace Njord.Domain.Sensors;

public enum SensorKind
{
    IndoorTemperature,
    IndoorHumidity,
}

public enum AggregationStrategy
{
    Average,
    Sum,
    Latest,
}

public sealed record SensorKindMetadata(
    SensorKind Kind,
    string Unit,
    double Min,
    double Max,
    AggregationStrategy Aggregation)
{
    public bool IsPlausible(double value) => value >= Min && value <= Max;

    private static readonly Dictionary<SensorKind, SensorKindMetadata> Registry = new()
    {
        [SensorKind.IndoorTemperature] = new(SensorKind.IndoorTemperature, "°C", -10, 60, AggregationStrategy.Average),
        [SensorKind.IndoorHumidity] = new(SensorKind.IndoorHumidity, "%", 0, 100, AggregationStrategy.Average),
    };

    public static SensorKindMetadata Get(SensorKind kind) => Registry[kind];

    public static bool TryGet(SensorKind kind, out SensorKindMetadata? metadata) =>
        Registry.TryGetValue(kind, out metadata);
}
