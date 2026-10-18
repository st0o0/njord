using Njord.Model;
namespace Njord.Forecast;

public sealed record DailyForecastPoint(
    DateOnly Date,
    IReadOnlyDictionary<ParameterDef, ForecastValue> Values)
{
    public ForecastValue Get(ParameterDef parameter) => Values.GetValueOrDefault(parameter);

    public bool HasAnyValue => Values.Values.Any(v => v.HasValue);
}
