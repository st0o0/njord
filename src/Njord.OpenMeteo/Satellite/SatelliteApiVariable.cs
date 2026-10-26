using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Satellite;

public enum SatelliteApiVariable
{
    [JsonStringEnumMemberName("shortwave_radiation")]
    GlobalHorizontal,
    [JsonStringEnumMemberName("direct_radiation")]
    DirectRadiation,
    [JsonStringEnumMemberName("diffuse_radiation")]
    DiffuseRadiation,
    [JsonStringEnumMemberName("direct_normal_irradiance")]
    DirectNormal,
    [JsonStringEnumMemberName("global_tilted_irradiance")]
    GlobalTilted,
}
