using Njord.Core.Seasonal;

namespace Njord.OpenMeteo.Seasonal;

public static class SeasonalWeeklyApiVariableMappingExtensions
{
    public static SeasonalWeeklyApiVariable ToApiVariable(this SeasonalWeeklyParams param) => param switch
    {
        SeasonalWeeklyParams.TemperatureEfi => SeasonalWeeklyApiVariable.TemperatureEfi,
        SeasonalWeeklyParams.PrecipitationEfi => SeasonalWeeklyApiVariable.PrecipitationEfi,
        SeasonalWeeklyParams.TemperatureSot10 => SeasonalWeeklyApiVariable.TemperatureSot10,
        SeasonalWeeklyParams.TemperatureSot90 => SeasonalWeeklyApiVariable.TemperatureSot90,
        SeasonalWeeklyParams.PrecipitationSot90 => SeasonalWeeklyApiVariable.PrecipitationSot90,
        SeasonalWeeklyParams.TemperatureAnomalyGt0 => SeasonalWeeklyApiVariable.TemperatureAnomalyGt0,
        SeasonalWeeklyParams.TemperatureAnomalyGt1 => SeasonalWeeklyApiVariable.TemperatureAnomalyGt1,
        SeasonalWeeklyParams.TemperatureAnomalyGt2 => SeasonalWeeklyApiVariable.TemperatureAnomalyGt2,
        SeasonalWeeklyParams.TemperatureAnomalyLtM1 => SeasonalWeeklyApiVariable.TemperatureAnomalyLtM1,
        SeasonalWeeklyParams.TemperatureAnomalyLtM2 => SeasonalWeeklyApiVariable.TemperatureAnomalyLtM2,
        SeasonalWeeklyParams.PrecipitationAnomalyGt0 => SeasonalWeeklyApiVariable.PrecipitationAnomalyGt0,
        SeasonalWeeklyParams.PrecipitationAnomalyGt10 => SeasonalWeeklyApiVariable.PrecipitationAnomalyGt10,
        SeasonalWeeklyParams.PrecipitationAnomalyGt20 => SeasonalWeeklyApiVariable.PrecipitationAnomalyGt20,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<SeasonalWeeklyApiVariable> ToApiVariables(this SeasonalWeeklyParams flags) =>
        [.. Enum.GetValues<SeasonalWeeklyParams>().Where(p => p != SeasonalWeeklyParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this SeasonalWeeklyParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
