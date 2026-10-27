using System.Text.Json;
using System.Text.Json.Serialization;

namespace Njord.OpenMeteo.Weather;

public sealed record WeatherHourlyData(
    [property: JsonPropertyName("time")] long[] Time,
    [property: JsonPropertyName("temperature_2m")] double?[]? Temperature = null,
    [property: JsonPropertyName("apparent_temperature")] double?[]? ApparentTemperature = null,
    [property: JsonPropertyName("wind_speed_10m")] double?[]? WindSpeed = null,
    [property: JsonPropertyName("precipitation")] double?[]? Precipitation = null,
    [property: JsonPropertyName("precipitation_probability")] double?[]? PrecipitationProbability = null,
    [property: JsonPropertyName("cloud_cover")] double?[]? CloudCover = null,
    [property: JsonPropertyName("pressure_msl")] double?[]? Pressure = null,
    [property: JsonPropertyName("weather_code")] double?[]? WeatherCode = null,
    [property: JsonPropertyName("relative_humidity_2m")] double?[]? Humidity = null,
    [property: JsonPropertyName("dew_point_2m")] double?[]? DewPoint = null,
    [property: JsonPropertyName("uv_index")] double?[]? UvIndex = null,
    [property: JsonPropertyName("wind_gusts_10m")] double?[]? WindGusts = null,
    [property: JsonPropertyName("visibility")] double?[]? Visibility = null,
    [property: JsonPropertyName("cape")] double?[]? Cape = null,
    [property: JsonPropertyName("rain")] double?[]? Rain = null,
    [property: JsonPropertyName("snowfall")] double?[]? Snowfall = null,
    [property: JsonPropertyName("soil_temperature_0_to_10cm")] double?[]? SoilTemperature = null,
    [property: JsonPropertyName("freezing_level_height")] double?[]? FreezingLevelHeight = null,
    [property: JsonPropertyName("is_day")] double?[]? IsDay = null,
    [property: JsonPropertyName("shortwave_radiation")] double?[]? ShortwaveRadiation = null,
    [property: JsonPropertyName("direct_radiation")] double?[]? DirectRadiation = null,
    [property: JsonPropertyName("diffuse_radiation")] double?[]? DiffuseRadiation = null,
    [property: JsonPropertyName("direct_normal_irradiance")] double?[]? DirectNormalIrradiance = null,
    [property: JsonPropertyName("sunshine_duration")] double?[]? SunshineDuration = null,
    [property: JsonPropertyName("wind_direction_10m")] double?[]? WindDirection = null,
    [property: JsonPropertyName("global_tilted_irradiance")] double?[]? GlobalTiltedIrradiance = null,
    [property: JsonPropertyName("snow_depth")] double?[]? SnowDepth = null,
    [property: JsonPropertyName("wet_bulb_temperature_2m")] double?[]? WetBulbTemperature = null,
    [property: JsonPropertyName("snowfall_height")] double?[]? SnowfallHeight = null,
    [property: JsonPropertyName("uv_index_clear_sky")] double?[]? UvIndexClearSky = null,
    [property: JsonPropertyName("showers")] double?[]? Showers = null,
    [property: JsonPropertyName("soil_moisture_0_to_1cm")] double?[]? SoilMoisture0To1cm = null,
    [property: JsonPropertyName("soil_moisture_1_to_3cm")] double?[]? SoilMoisture1To3cm = null,
    [property: JsonPropertyName("et0_fao_evapotranspiration")] double?[]? Evapotranspiration = null,
    [property: JsonPropertyName("vapour_pressure_deficit")] double?[]? VapourPressureDeficit = null,
    [property: JsonPropertyName("boundary_layer_height")] double?[]? BoundaryLayerHeight = null,
    [property: JsonPropertyName("cloud_cover_low")] double?[]? CloudCoverLow = null,
    [property: JsonPropertyName("cloud_cover_mid")] double?[]? CloudCoverMid = null,
    [property: JsonPropertyName("cloud_cover_high")] double?[]? CloudCoverHigh = null,
    [property: JsonPropertyName("convective_inhibition")] double?[]? ConvectiveInhibition = null,
    [property: JsonPropertyName("lifted_index")] double?[]? LiftedIndex = null);

public sealed record WeatherResponse(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("hourly")] Dictionary<string, JsonElement>? Hourly);
