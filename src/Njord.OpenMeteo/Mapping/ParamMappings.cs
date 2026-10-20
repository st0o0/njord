using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.OpenMeteo.Mapping;

public static class WeatherParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this WeatherParam flags) =>
        [.. Enum.GetValues<WeatherParam>().Where(p => p != WeatherParam.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(WeatherParam param) => param switch
    {
        WeatherParam.Temperature => "temperature_2m",
        WeatherParam.ApparentTemperature => "apparent_temperature",
        WeatherParam.WindSpeed => "wind_speed_10m",
        WeatherParam.Precipitation => "precipitation",
        WeatherParam.PrecipitationProbability => "precipitation_probability",
        WeatherParam.CloudCover => "cloud_cover",
        WeatherParam.Pressure => "pressure_msl",
        WeatherParam.WeatherCode => "weather_code",
        WeatherParam.Humidity => "relative_humidity_2m",
        WeatherParam.DewPoint => "dew_point_2m",
        WeatherParam.UvIndex => "uv_index",
        WeatherParam.EnsembleStdDev => "temperature_2m_spread",
        WeatherParam.WindGusts => "wind_gusts_10m",
        WeatherParam.Visibility => "visibility",
        WeatherParam.Cape => "cape",
        WeatherParam.Rain => "rain",
        WeatherParam.Snowfall => "snowfall",
        WeatherParam.SoilTemperature => "soil_temperature_0cm",
        WeatherParam.FreezingLevelHeight => "freezing_level_height",
        WeatherParam.IsDay => "is_day",
        WeatherParam.ShortwaveRadiation => "shortwave_radiation",
        WeatherParam.DirectRadiation => "direct_radiation",
        WeatherParam.DiffuseRadiation => "diffuse_radiation",
        WeatherParam.DirectNormalIrradiance => "direct_normal_irradiance",
        WeatherParam.SunshineDuration => "sunshine_duration",
        WeatherParam.WindDirection => "wind_direction_10m",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class AirQualityParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this AirQualityParam flags) =>
        [.. Enum.GetValues<AirQualityParam>().Where(p => p != AirQualityParam.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(AirQualityParam param) => param switch
    {
        AirQualityParam.Pm25 => "pm2_5",
        AirQualityParam.Pm10 => "pm10",
        AirQualityParam.Ozone => "ozone",
        AirQualityParam.NitrogenDioxide => "nitrogen_dioxide",
        AirQualityParam.EuropeanAqi => "european_aqi",
        AirQualityParam.BirchPollen => "birch_pollen",
        AirQualityParam.GrassPollen => "grass_pollen",
        AirQualityParam.Alder => "alder_pollen",
        AirQualityParam.Mugwort => "mugwort_pollen",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class FloodParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this FloodParam flags) =>
        [.. Enum.GetValues<FloodParam>().Where(p => p != FloodParam.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(FloodParam param) => param switch
    {
        FloodParam.Discharge => "river_discharge",
        FloodParam.DischargeMean => "river_discharge_mean",
        FloodParam.DischargeMedian => "river_discharge_median",
        FloodParam.DischargeMax => "river_discharge_max",
        FloodParam.DischargeMin => "river_discharge_min",
        FloodParam.DischargeP25 => "river_discharge_p25",
        FloodParam.DischargeP75 => "river_discharge_p75",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class MarineParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this MarineParam flags) =>
        [.. Enum.GetValues<MarineParam>().Where(p => p != MarineParam.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(MarineParam param) => param switch
    {
        MarineParam.WaveHeight => "wave_height",
        MarineParam.WavePeriod => "wave_period",
        MarineParam.WaveDirection => "wave_direction",
        MarineParam.WaterTemperature => "sea_surface_temperature",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
