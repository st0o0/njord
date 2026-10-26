using Njord.Forecast.Marine;

namespace Njord.OpenMeteo.Marine;

public static class MarineApiVariableMappingExtensions
{
    public static MarineApiVariable ToApiVariable(this MarineParams param) => param switch
    {
        MarineParams.WaveHeight => MarineApiVariable.WaveHeight,
        MarineParams.WavePeriod => MarineApiVariable.WavePeriod,
        MarineParams.WaveDirection => MarineApiVariable.WaveDirection,
        MarineParams.WaterTemperature => MarineApiVariable.WaterTemperature,
        MarineParams.WindWaveHeight => MarineApiVariable.WindWaveHeight,
        MarineParams.WindWavePeriod => MarineApiVariable.WindWavePeriod,
        MarineParams.WindWaveDirection => MarineApiVariable.WindWaveDirection,
        MarineParams.WindWavePeakPeriod => MarineApiVariable.WindWavePeakPeriod,
        MarineParams.SwellWaveHeight => MarineApiVariable.SwellWaveHeight,
        MarineParams.SwellWavePeriod => MarineApiVariable.SwellWavePeriod,
        MarineParams.SwellWaveDirection => MarineApiVariable.SwellWaveDirection,
        MarineParams.SwellWavePeakPeriod => MarineApiVariable.SwellWavePeakPeriod,
        MarineParams.SeaLevelHeight => MarineApiVariable.SeaLevelHeight,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<MarineApiVariable> ToApiVariables(this MarineParams flags) =>
        [.. Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this MarineParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
