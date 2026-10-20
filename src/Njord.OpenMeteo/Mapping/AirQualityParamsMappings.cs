using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.OpenMeteo.Mapping;

public static class AirQualityParamsMappings
{
    public static IReadOnlyList<string> ToApiNames(this AirQualityParams flags) =>
        [.. Enum.GetValues<AirQualityParams>().Where(p => p != AirQualityParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(AirQualityParams param) => param switch
    {
        AirQualityParams.Pm25 => "pm2_5",
        AirQualityParams.Pm10 => "pm10",
        AirQualityParams.Ozone => "ozone",
        AirQualityParams.NitrogenDioxide => "nitrogen_dioxide",
        AirQualityParams.EuropeanAqi => "european_aqi",
        AirQualityParams.BirchPollen => "birch_pollen",
        AirQualityParams.GrassPollen => "grass_pollen",
        AirQualityParams.Alder => "alder_pollen",
        AirQualityParams.Mugwort => "mugwort_pollen",
        AirQualityParams.CarbonMonoxide => "carbon_monoxide",
        AirQualityParams.SulphurDioxide => "sulphur_dioxide",
        AirQualityParams.Dust => "dust",
        AirQualityParams.Ammonia => "ammonia",
        AirQualityParams.OlivePollen => "olive_pollen",
        AirQualityParams.UsAqi => "us_aqi",
        AirQualityParams.UvIndex => "uv_index",
        AirQualityParams.UvIndexClearSky => "uv_index_clear_sky",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
