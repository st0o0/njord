using Njord.Core.Forecast;

namespace Njord.OpenMeteo.Ensemble;

public static class EnsembleApiVariableMappingExtensions
{
    public static EnsembleApiVariable ToApiVariable(this EnsembleParams param) => param switch
    {
        EnsembleParams.TemperatureSpread => EnsembleApiVariable.TemperatureSpread,
        EnsembleParams.PrecipitationSpread => EnsembleApiVariable.PrecipitationSpread,
        EnsembleParams.WindSpeedSpread => EnsembleApiVariable.WindSpeedSpread,
        EnsembleParams.PressureMslSpread => EnsembleApiVariable.PressureMslSpread,
        EnsembleParams.CloudCoverSpread => EnsembleApiVariable.CloudCoverSpread,
        EnsembleParams.WindGustsSpread => EnsembleApiVariable.WindGustsSpread,
        EnsembleParams.CapeSpread => EnsembleApiVariable.CapeSpread,
        EnsembleParams.SnowfallSpread => EnsembleApiVariable.SnowfallSpread,
        EnsembleParams.RainSpread => EnsembleApiVariable.RainSpread,
        EnsembleParams.DewPointSpread => EnsembleApiVariable.DewPointSpread,
        EnsembleParams.HumiditySpread => EnsembleApiVariable.HumiditySpread,
        EnsembleParams.SurfacePressureSpread => EnsembleApiVariable.SurfacePressureSpread,
        EnsembleParams.WindDirectionSpread => EnsembleApiVariable.WindDirectionSpread,
        EnsembleParams.WetBulbTemperatureSpread => EnsembleApiVariable.WetBulbTemperatureSpread,
        EnsembleParams.WindSpeed100mSpread => EnsembleApiVariable.WindSpeed100mSpread,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<EnsembleApiVariable> ToApiVariables(this EnsembleParams flags) =>
        [.. Enum.GetValues<EnsembleParams>()
            .Where(p => p != EnsembleParams.None && flags.HasFlag(p))
            .Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this EnsembleParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
