using Njord.Core.Configuration;
using System.Collections.Frozen;
using Njord.Model;

namespace Njord.Mqtt.Mapping;

internal static class ParameterMqttKeys
{
    private static readonly FrozenDictionary<ParameterDef, string> Keys = new Dictionary<ParameterDef, string>
    {
        [ParameterRegistry.Temperature2m] = "temperature",
        [ParameterRegistry.ApparentTemperature] = "apparent_temperature",
        [ParameterRegistry.RelativeHumidity2m] = "relative_humidity",
        [ParameterRegistry.DewPoint2m] = "dewpoint",
        [ParameterRegistry.Precipitation] = "precipitation",
        [ParameterRegistry.Rain] = "rain",
        [ParameterRegistry.Showers] = "showers",
        [ParameterRegistry.Snowfall] = "snowfall",
        [ParameterRegistry.SnowDepth] = "snow_depth",
        [ParameterRegistry.WeatherCode] = "weather_code",
        [ParameterRegistry.CloudCover] = "cloud_cover",
        [ParameterRegistry.CloudCoverLow] = "cloud_cover_low",
        [ParameterRegistry.CloudCoverMid] = "cloud_cover_mid",
        [ParameterRegistry.CloudCoverHigh] = "cloud_cover_high",
        [ParameterRegistry.PressureMsl] = "pressure_msl",
        [ParameterRegistry.SurfacePressure] = "surface_pressure",
        [ParameterRegistry.Visibility] = "visibility",
        [ParameterRegistry.IsDay] = "is_day",
        [ParameterRegistry.PrecipitationProbability] = "precipitation_probability",
        [ParameterRegistry.WindSpeed10m] = "wind_speed_10m",
        [ParameterRegistry.WindSpeed80m] = "wind_speed_80m",
        [ParameterRegistry.WindSpeed120m] = "wind_speed_120m",
        [ParameterRegistry.WindSpeed180m] = "wind_speed_180m",
        [ParameterRegistry.WindDirection10m] = "wind_direction_10m",
        [ParameterRegistry.WindDirection80m] = "wind_direction_80m",
        [ParameterRegistry.WindDirection120m] = "wind_direction_120m",
        [ParameterRegistry.WindDirection180m] = "wind_direction_180m",
        [ParameterRegistry.WindGusts10m] = "wind_gusts_10m",
        [ParameterRegistry.Cape] = "cape",
        [ParameterRegistry.FreezingLevelHeight] = "freezing_level_height",
        [ParameterRegistry.VapourPressureDeficit] = "vapour_pressure_deficit",

        [ParameterRegistry.Temperature2mMax] = "temperature_max",
        [ParameterRegistry.Temperature2mMin] = "temperature_min",
        [ParameterRegistry.ApparentTemperatureMax] = "apparent_temperature_max",
        [ParameterRegistry.ApparentTemperatureMin] = "apparent_temperature_min",
        [ParameterRegistry.DailyWeatherCode] = "weather_code_daily",
        [ParameterRegistry.PrecipitationSum] = "precipitation_sum",
        [ParameterRegistry.RainSum] = "rain_sum",
        [ParameterRegistry.ShowersSum] = "showers_sum",
        [ParameterRegistry.SnowfallSum] = "snowfall_sum",
        [ParameterRegistry.PrecipitationHours] = "precipitation_hours",
        [ParameterRegistry.PrecipitationProbabilityMax] = "precipitation_probability_max",
        [ParameterRegistry.WindSpeed10mMax] = "wind_speed_max",
        [ParameterRegistry.WindGusts10mMax] = "wind_gusts_max",
        [ParameterRegistry.WindDirection10mDominant] = "wind_direction_dominant",
        [ParameterRegistry.Sunrise] = "sunrise",
        [ParameterRegistry.Sunset] = "sunset",
        [ParameterRegistry.DaylightDuration] = "daylight_duration",

        [ParameterRegistry.ShortwaveRadiation] = "shortwave_radiation",
        [ParameterRegistry.DirectRadiation] = "direct_radiation",
        [ParameterRegistry.DiffuseRadiation] = "diffuse_radiation",
        [ParameterRegistry.DirectNormalIrradiance] = "direct_normal_irradiance",
        [ParameterRegistry.GlobalTiltedIrradiance] = "global_tilted_irradiance",
        [ParameterRegistry.TerrestrialRadiation] = "terrestrial_radiation",
        [ParameterRegistry.SunshineDuration] = "sunshine_duration",
        [ParameterRegistry.UvIndex] = "uv_index",
        [ParameterRegistry.UvIndexClearSky] = "uv_index_clear_sky",

        [ParameterRegistry.ShortwaveRadiationSum] = "shortwave_radiation_sum",
        [ParameterRegistry.DailySunshineDuration] = "sunshine_duration_daily",
        [ParameterRegistry.UvIndexMax] = "uv_index_max",
        [ParameterRegistry.UvIndexClearSkyMax] = "uv_index_clear_sky_max",

        [ParameterRegistry.SoilTemperature0cm] = "soil_temperature_0cm",
        [ParameterRegistry.SoilTemperature6cm] = "soil_temperature_6cm",
        [ParameterRegistry.SoilTemperature18cm] = "soil_temperature_18cm",
        [ParameterRegistry.SoilTemperature54cm] = "soil_temperature_54cm",
        [ParameterRegistry.SoilMoisture0To1cm] = "soil_moisture_0_to_1cm",
        [ParameterRegistry.SoilMoisture1To3cm] = "soil_moisture_1_to_3cm",
        [ParameterRegistry.SoilMoisture3To9cm] = "soil_moisture_3_to_9cm",
        [ParameterRegistry.SoilMoisture9To27cm] = "soil_moisture_9_to_27cm",
        [ParameterRegistry.SoilMoisture27To81cm] = "soil_moisture_27_to_81cm",
        [ParameterRegistry.Evapotranspiration] = "evapotranspiration",
        [ParameterRegistry.Et0FaoEvapotranspiration] = "et0_fao_evapotranspiration",

        [ParameterRegistry.DailyEt0FaoEvapotranspiration] = "et0_fao_evapotranspiration_daily",
    }.ToFrozenDictionary();

    public static string ToMqttKey(this ParameterDef parameter)
        => Keys.TryGetValue(parameter, out var key)
            ? key
            : throw new InvalidOperationException($"No MQTT key is defined for parameter '{parameter.ApiName}' ({parameter.Granularity})");
}
