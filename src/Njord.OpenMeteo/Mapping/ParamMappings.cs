using Njord.Forecast.AirQuality;
using Njord.Forecast.Flood;
using Njord.Forecast.Marine;
using Njord.Forecast.Weather;

namespace Njord.OpenMeteo.Mapping;

public static class WeatherParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this WeatherParams flags) =>
        [.. Enum.GetValues<WeatherParams>().Where(p => p != WeatherParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(WeatherParams param) => param switch
    {
        WeatherParams.Temperature => "temperature_2m",
        WeatherParams.ApparentTemperature => "apparent_temperature",
        WeatherParams.WindSpeed => "wind_speed_10m",
        WeatherParams.Precipitation => "precipitation",
        WeatherParams.PrecipitationProbability => "precipitation_probability",
        WeatherParams.CloudCover => "cloud_cover",
        WeatherParams.Pressure => "pressure_msl",
        WeatherParams.WeatherCode => "weather_code",
        WeatherParams.Humidity => "relative_humidity_2m",
        WeatherParams.DewPoint => "dew_point_2m",
        WeatherParams.UvIndex => "uv_index",
        WeatherParams.EnsembleStdDev => "temperature_2m_spread",
        WeatherParams.WindGusts => "wind_gusts_10m",
        WeatherParams.Visibility => "visibility",
        WeatherParams.Cape => "cape",
        WeatherParams.Rain => "rain",
        WeatherParams.Snowfall => "snowfall",
        WeatherParams.SoilTemperature => "soil_temperature_0_to_10cm",
        WeatherParams.FreezingLevelHeight => "freezing_level_height",
        WeatherParams.IsDay => "is_day",
        WeatherParams.ShortwaveRadiation => "shortwave_radiation",
        WeatherParams.DirectRadiation => "direct_radiation",
        WeatherParams.DiffuseRadiation => "diffuse_radiation",
        WeatherParams.DirectNormalIrradiance => "direct_normal_irradiance",
        WeatherParams.SunshineDuration => "sunshine_duration",
        WeatherParams.WindDirection => "wind_direction_10m",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class AirQualityParamMappings
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
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class FloodParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this FloodParams flags) =>
        [.. Enum.GetValues<FloodParams>().Where(p => p != FloodParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(FloodParams param) => param switch
    {
        FloodParams.Discharge => "river_discharge",
        FloodParams.DischargeMean => "river_discharge_mean",
        FloodParams.DischargeMedian => "river_discharge_median",
        FloodParams.DischargeMax => "river_discharge_max",
        FloodParams.DischargeMin => "river_discharge_min",
        FloodParams.DischargeP25 => "river_discharge_p25",
        FloodParams.DischargeP75 => "river_discharge_p75",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}

public static class MarineParamMappings
{
    public static IReadOnlyList<string> ToApiNames(this MarineParams flags) =>
        [.. Enum.GetValues<MarineParams>().Where(p => p != MarineParams.None && flags.HasFlag(p)).Select(ToApiName)];

    private static string ToApiName(MarineParams param) => param switch
    {
        MarineParams.WaveHeight => "wave_height",
        MarineParams.WavePeriod => "wave_period",
        MarineParams.WaveDirection => "wave_direction",
        MarineParams.WaterTemperature => "sea_surface_temperature",
        _ => throw new ArgumentOutOfRangeException(nameof(param), param, null),
    };
}
