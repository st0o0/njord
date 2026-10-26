using System.Text.Json.Serialization;

namespace Njord.OpenMeteo;

public enum AirQualityApiVariable
{
    [JsonStringEnumMemberName("pm2_5")]
    Pm25,
    [JsonStringEnumMemberName("pm10")]
    Pm10,
    [JsonStringEnumMemberName("ozone")]
    Ozone,
    [JsonStringEnumMemberName("nitrogen_dioxide")]
    NitrogenDioxide,
    [JsonStringEnumMemberName("european_aqi")]
    EuropeanAqi,
    [JsonStringEnumMemberName("birch_pollen")]
    BirchPollen,
    [JsonStringEnumMemberName("grass_pollen")]
    GrassPollen,
    [JsonStringEnumMemberName("alder_pollen")]
    Alder,
    [JsonStringEnumMemberName("mugwort_pollen")]
    Mugwort,
    [JsonStringEnumMemberName("carbon_monoxide")]
    CarbonMonoxide,
    [JsonStringEnumMemberName("sulphur_dioxide")]
    SulphurDioxide,
    [JsonStringEnumMemberName("dust")]
    Dust,
    [JsonStringEnumMemberName("ammonia")]
    Ammonia,
    [JsonStringEnumMemberName("olive_pollen")]
    OlivePollen,
    [JsonStringEnumMemberName("us_aqi")]
    UsAqi,
    [JsonStringEnumMemberName("uv_index")]
    UvIndex,
    [JsonStringEnumMemberName("uv_index_clear_sky")]
    UvIndexClearSky,
}
