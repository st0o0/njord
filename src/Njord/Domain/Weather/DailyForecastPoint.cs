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
}
