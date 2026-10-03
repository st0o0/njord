namespace Njord.Domain.Weather;

public sealed record ForecastPoint(
    DateTimeOffset ValidAt,
    IReadOnlyDictionary<ParameterDef, double?> Values)
{
    public double? Get(ParameterDef parameter)
        => Values.GetValueOrDefault(parameter);

    public bool HasAnyValue => Values.Values.Any(v => v is not null);
}
