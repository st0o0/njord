using Njord.Model;
namespace Njord.Forecast;

public sealed record DailyForecastPoint(
    DateOnly Date,
    IReadOnlyDictionary<ParameterDef, ForecastValue> Values)
{
    public ForecastValue Get(ParameterDef parameter)
        => Values.TryGetValue(parameter, out var v) ? v : default;

    public bool HasAnyValue => Values.Values.Any(v => v.HasValue);
}
