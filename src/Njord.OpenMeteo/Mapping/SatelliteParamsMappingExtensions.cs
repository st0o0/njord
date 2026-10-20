using Njord.Forecast.Solar;

namespace Njord.OpenMeteo.Mapping;

public static class SatelliteParamsMappingExtensions
{
    public static IReadOnlyList<string> ToApiNames(this SatelliteParams flags) =>
        [.. Enum.GetValues<SatelliteParams>().Where(p => p != SatelliteParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(SatelliteParams param) => param switch
    {
        SatelliteParams.GlobalHorizontal => "shortwave_radiation",
        SatelliteParams.DirectRadiation => "direct_radiation",
        SatelliteParams.DiffuseRadiation => "diffuse_radiation",
        SatelliteParams.DirectNormal => "direct_normal_irradiance",
        SatelliteParams.GlobalTilted => "global_tilted_irradiance",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
