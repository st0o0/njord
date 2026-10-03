namespace Njord.Domain.Weather;

public sealed record DailyForecastPoint(
    DateOnly Date,
    IReadOnlyDictionary<ParameterDef, double?> NumericValues,
    IReadOnlyDictionary<ParameterDef, string?> MetaValues)
{
    public double? GetNumeric(ParameterDef parameter)
        => NumericValues.GetValueOrDefault(parameter);

    public string? GetMeta(ParameterDef parameter)
        => MetaValues.GetValueOrDefault(parameter);

    public bool HasAnyValue =>
        NumericValues.Values.Any(v => v is not null) ||
        MetaValues.Values.Any(v => v is not null);
}
