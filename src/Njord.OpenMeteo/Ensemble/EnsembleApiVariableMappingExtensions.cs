using Njord.Core.Forecast;

namespace Njord.OpenMeteo.Ensemble;

public static class EnsembleApiVariableMappingExtensions
{
    public static EnsembleApiVariable ToApiVariable(this EnsembleParams param) => param switch
    {
        EnsembleParams.TemperatureSpread => EnsembleApiVariable.TemperatureSpread,
        EnsembleParams.PrecipitationSpread => EnsembleApiVariable.PrecipitationSpread,
        EnsembleParams.WindSpeedSpread => EnsembleApiVariable.WindSpeedSpread,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<EnsembleApiVariable> ToApiVariables(this EnsembleParams flags) =>
        [.. Enum.GetValues<EnsembleParams>()
            .Where(p => p != EnsembleParams.None && p != EnsembleParams.Models && flags.HasFlag(p))
            .Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this EnsembleParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
