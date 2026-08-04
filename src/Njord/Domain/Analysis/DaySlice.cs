using Njord.Domain.Weather;

namespace Njord.Domain.Analysis;

public sealed record DaySlice(
    int DayOffset,
    IReadOnlyDictionary<ParameterDef, double?> DayMeans,
    IReadOnlyDictionary<ParameterDef, double?> NightMeans,
    IReadOnlyDictionary<ParameterDef, double?> FullDayMeans,
    int DaylightHoursCount,
    int NighttimeHoursCount);
