using Njord.Weather;

namespace Njord.Indices;

public sealed record PeriodMeans(
    IReadOnlyDictionary<ParameterDef, double?> Daylight,
    IReadOnlyDictionary<ParameterDef, double?> Night,
    IReadOnlyDictionary<ParameterDef, double?> FullDay)
{
    public IReadOnlyDictionary<ParameterDef, double?> For(DayPeriod period) => period switch
    {
        DayPeriod.Daylight => Daylight,
        DayPeriod.Night => Night,
        DayPeriod.FullDay => FullDay,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
    };
}
