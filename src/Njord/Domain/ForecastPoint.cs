namespace Njord.Domain;

public sealed record ForecastPoint(
    DateTimeOffset ValidAt,
    IReadOnlyDictionary<ParameterDef, double?> Values)
{
    public double? Get(ParameterDef parameter)
        => Values.TryGetValue(parameter, out var value) ? value : null;

    public bool HasAnyValue => Values.Values.Any(v => v is not null);
}
