using Njord.Forecast.Seasonal;

namespace Njord.OpenMeteo.Seasonal;

public static class SeasonalApiVariableMappingExtensions
{
    public static SeasonalApiVariable ToApiVariable(this SeasonalParams param) => param switch
    {
        SeasonalParams.TemperatureAnomaly => SeasonalApiVariable.TemperatureAnomaly,
        SeasonalParams.PrecipitationAnomaly => SeasonalApiVariable.PrecipitationAnomaly,
        SeasonalParams.PrecipitationMean => SeasonalApiVariable.PrecipitationMean,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<SeasonalApiVariable> ToApiVariables(this SeasonalParams flags) =>
        [.. Enum.GetValues<SeasonalParams>().Where(p => p != SeasonalParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this SeasonalParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
