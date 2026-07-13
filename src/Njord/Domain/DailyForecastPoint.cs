namespace Njord.Domain;

public sealed record DailyForecastPoint(
    DateOnly Date,
    IReadOnlyDictionary<ParameterDef, object?> Values)
{
    public object? Get(ParameterDef parameter)
        => Values.GetValueOrDefault(parameter);
}
