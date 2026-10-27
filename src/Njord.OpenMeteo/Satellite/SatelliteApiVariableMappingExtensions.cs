using Njord.Core.Solar;

namespace Njord.OpenMeteo.Satellite;

public static class SatelliteApiVariableMappingExtensions
{
    public static SatelliteApiVariable ToApiVariable(this SatelliteParams param) => param switch
    {
        SatelliteParams.GlobalHorizontal => SatelliteApiVariable.GlobalHorizontal,
        SatelliteParams.DirectRadiation => SatelliteApiVariable.DirectRadiation,
        SatelliteParams.DiffuseRadiation => SatelliteApiVariable.DiffuseRadiation,
        SatelliteParams.DirectNormal => SatelliteApiVariable.DirectNormal,
        SatelliteParams.GlobalTilted => SatelliteApiVariable.GlobalTilted,
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };

    public static IReadOnlyList<SatelliteApiVariable> ToApiVariables(this SatelliteParams flags) =>
        [.. Enum.GetValues<SatelliteParams>().Where(p => p != SatelliteParams.None && flags.HasFlag(p)).Select(ToApiVariable)];

    public static IReadOnlyList<string> ToApiNames(this SatelliteParams flags) =>
        [.. flags.ToApiVariables().Select(v => v.ToApiName())];
}
