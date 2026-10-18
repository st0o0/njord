using System.Collections.Frozen;

namespace Njord.Weather;

public static class ParameterRegistry
{
    // Declared first: static field initializers run in textual order and every parameter registers itself here.
    private static readonly List<ParameterDef> Catalog = [];

    // === Weather group: Hourly ===
    public static readonly ParameterDef Temperature2m = Hourly("temperature_2m", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef ApparentTemperature = Hourly("apparent_temperature", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef RelativeHumidity2m = Hourly("relative_humidity_2m", "%", ParameterGroup.Weather);
    public static readonly ParameterDef DewPoint2m = Hourly("dew_point_2m", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef Precipitation = Hourly("precipitation", "mm", ParameterGroup.Weather);
    public static readonly ParameterDef Rain = Hourly("rain", "mm", ParameterGroup.Weather);
    public static readonly ParameterDef Showers = Hourly("showers", "mm", ParameterGroup.Weather);
    public static readonly ParameterDef Snowfall = Hourly("snowfall", "cm", ParameterGroup.Weather);
    public static readonly ParameterDef SnowDepth = Hourly("snow_depth", "m", ParameterGroup.Weather);
    public static readonly ParameterDef WeatherCode = Hourly("weather_code", "wmo code", ParameterGroup.Weather);
    public static readonly ParameterDef CloudCover = Hourly("cloud_cover", "%", ParameterGroup.Weather);
    public static readonly ParameterDef CloudCoverLow = Hourly("cloud_cover_low", "%", ParameterGroup.Weather);
    public static readonly ParameterDef CloudCoverMid = Hourly("cloud_cover_mid", "%", ParameterGroup.Weather);
    public static readonly ParameterDef CloudCoverHigh = Hourly("cloud_cover_high", "%", ParameterGroup.Weather);
    public static readonly ParameterDef PressureMsl = Hourly("pressure_msl", "hPa", ParameterGroup.Weather);
    public static readonly ParameterDef SurfacePressure = Hourly("surface_pressure", "hPa", ParameterGroup.Weather);
    public static readonly ParameterDef Visibility = Hourly("visibility", "m", ParameterGroup.Weather);
    public static readonly ParameterDef IsDay = Hourly("is_day", "", ParameterGroup.Weather);
    public static readonly ParameterDef PrecipitationProbability = Hourly("precipitation_probability", "%", ParameterGroup.Weather);
    public static readonly ParameterDef WindSpeed10m = Hourly("wind_speed_10m", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef WindSpeed80m = Hourly("wind_speed_80m", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef WindSpeed120m = Hourly("wind_speed_120m", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef WindSpeed180m = Hourly("wind_speed_180m", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef WindDirection10m = Hourly("wind_direction_10m", "°", ParameterGroup.Weather);
    public static readonly ParameterDef WindDirection80m = Hourly("wind_direction_80m", "°", ParameterGroup.Weather);
    public static readonly ParameterDef WindDirection120m = Hourly("wind_direction_120m", "°", ParameterGroup.Weather);
    public static readonly ParameterDef WindDirection180m = Hourly("wind_direction_180m", "°", ParameterGroup.Weather);
    public static readonly ParameterDef WindGusts10m = Hourly("wind_gusts_10m", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef Cape = Hourly("cape", "J/kg", ParameterGroup.Weather);
    public static readonly ParameterDef FreezingLevelHeight = Hourly("freezing_level_height", "m", ParameterGroup.Weather);
    public static readonly ParameterDef VapourPressureDeficit = Hourly("vapour_pressure_deficit", "kPa", ParameterGroup.Weather);

    // === Weather group: Daily ===
    public static readonly ParameterDef Temperature2mMax = Daily("temperature_2m_max", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef Temperature2mMin = Daily("temperature_2m_min", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef ApparentTemperatureMax = Daily("apparent_temperature_max", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef ApparentTemperatureMin = Daily("apparent_temperature_min", "°C", ParameterGroup.Weather);
    public static readonly ParameterDef DailyWeatherCode = Daily("weather_code", "wmo code", ParameterGroup.Weather);
    public static readonly ParameterDef PrecipitationSum = Daily("precipitation_sum", "mm", ParameterGroup.Weather);
    public static readonly ParameterDef RainSum = Daily("rain_sum", "mm", ParameterGroup.Weather);
    public static readonly ParameterDef ShowersSum = Daily("showers_sum", "mm", ParameterGroup.Weather);
    public static readonly ParameterDef SnowfallSum = Daily("snowfall_sum", "cm", ParameterGroup.Weather);
    public static readonly ParameterDef PrecipitationHours = Daily("precipitation_hours", "h", ParameterGroup.Weather);
    public static readonly ParameterDef PrecipitationProbabilityMax = Daily("precipitation_probability_max", "%", ParameterGroup.Weather);
    public static readonly ParameterDef WindSpeed10mMax = Daily("wind_speed_10m_max", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef WindGusts10mMax = Daily("wind_gusts_10m_max", "m/s", ParameterGroup.Weather);
    public static readonly ParameterDef WindDirection10mDominant = Daily("wind_direction_10m_dominant", "°", ParameterGroup.Weather);
    public static readonly ParameterDef Sunrise = Daily("sunrise", "", ParameterGroup.Weather, ParameterValueType.TimeString);
    public static readonly ParameterDef Sunset = Daily("sunset", "", ParameterGroup.Weather, ParameterValueType.TimeString);
    public static readonly ParameterDef DaylightDuration = Daily("daylight_duration", "s", ParameterGroup.Weather);

    // === Solar group: Hourly ===
    public static readonly ParameterDef ShortwaveRadiation = Hourly("shortwave_radiation", "W/m²", ParameterGroup.Solar);
    public static readonly ParameterDef DirectRadiation = Hourly("direct_radiation", "W/m²", ParameterGroup.Solar);
    public static readonly ParameterDef DiffuseRadiation = Hourly("diffuse_radiation", "W/m²", ParameterGroup.Solar);
    public static readonly ParameterDef DirectNormalIrradiance = Hourly("direct_normal_irradiance", "W/m²", ParameterGroup.Solar);
    public static readonly ParameterDef GlobalTiltedIrradiance = Hourly("global_tilted_irradiance", "W/m²", ParameterGroup.Solar);
    public static readonly ParameterDef TerrestrialRadiation = Hourly("terrestrial_radiation", "W/m²", ParameterGroup.Solar);
    public static readonly ParameterDef SunshineDuration = Hourly("sunshine_duration", "s", ParameterGroup.Solar);
    public static readonly ParameterDef UvIndex = Hourly("uv_index", "", ParameterGroup.Solar);
    public static readonly ParameterDef UvIndexClearSky = Hourly("uv_index_clear_sky", "", ParameterGroup.Solar);

    // === Solar group: Daily ===
    public static readonly ParameterDef ShortwaveRadiationSum = Daily("shortwave_radiation_sum", "MJ/m²", ParameterGroup.Solar);
    public static readonly ParameterDef DailySunshineDuration = Daily("sunshine_duration", "s", ParameterGroup.Solar);
    public static readonly ParameterDef UvIndexMax = Daily("uv_index_max", "", ParameterGroup.Solar);
    public static readonly ParameterDef UvIndexClearSkyMax = Daily("uv_index_clear_sky_max", "", ParameterGroup.Solar);

    // === Soil group: Hourly ===
    public static readonly ParameterDef SoilTemperature0cm = Hourly("soil_temperature_0cm", "°C", ParameterGroup.Soil);
    public static readonly ParameterDef SoilTemperature6cm = Hourly("soil_temperature_6cm", "°C", ParameterGroup.Soil);
    public static readonly ParameterDef SoilTemperature18cm = Hourly("soil_temperature_18cm", "°C", ParameterGroup.Soil);
    public static readonly ParameterDef SoilTemperature54cm = Hourly("soil_temperature_54cm", "°C", ParameterGroup.Soil);
    public static readonly ParameterDef SoilMoisture0To1cm = Hourly("soil_moisture_0_to_1cm", "m³/m³", ParameterGroup.Soil);
    public static readonly ParameterDef SoilMoisture1To3cm = Hourly("soil_moisture_1_to_3cm", "m³/m³", ParameterGroup.Soil);
    public static readonly ParameterDef SoilMoisture3To9cm = Hourly("soil_moisture_3_to_9cm", "m³/m³", ParameterGroup.Soil);
    public static readonly ParameterDef SoilMoisture9To27cm = Hourly("soil_moisture_9_to_27cm", "m³/m³", ParameterGroup.Soil);
    public static readonly ParameterDef SoilMoisture27To81cm = Hourly("soil_moisture_27_to_81cm", "m³/m³", ParameterGroup.Soil);
    public static readonly ParameterDef Evapotranspiration = Hourly("evapotranspiration", "mm", ParameterGroup.Soil);
    public static readonly ParameterDef Et0FaoEvapotranspiration = Hourly("et0_fao_evapotranspiration", "mm", ParameterGroup.Soil);

    // === Soil group: Daily ===
    public static readonly ParameterDef DailyEt0FaoEvapotranspiration = Daily("et0_fao_evapotranspiration", "mm", ParameterGroup.Soil);

    // Declared after the parameters: the first declaration of an API name (the hourly one) wins.
    private static readonly FrozenDictionary<string, ParameterDef> ByName = BuildByName();

    public static IReadOnlyCollection<ParameterDef> All => Catalog;

    public static IReadOnlyList<ParameterDef> GetByGroup(ParameterGroup group)
        => [.. Catalog.Where(p => p.Group == group)];

    public static ParameterDef? GetByApiName(string name)
        => ByName.GetValueOrDefault(name);

    private static ParameterDef Hourly(string apiName, string unit, ParameterGroup group, ParameterValueType valueType = ParameterValueType.Numeric)
        => Register(new ParameterDef(apiName, unit, group, ParameterGranularity.Hourly, valueType));

    private static ParameterDef Daily(string apiName, string unit, ParameterGroup group, ParameterValueType valueType = ParameterValueType.Numeric)
        => Register(new ParameterDef(apiName, unit, group, ParameterGranularity.Daily, valueType));

    private static ParameterDef Register(ParameterDef def)
    {
        Catalog.Add(def);
        return def;
    }

    private static FrozenDictionary<string, ParameterDef> BuildByName()
    {
        var byName = new Dictionary<string, ParameterDef>(StringComparer.Ordinal);
        foreach (var p in Catalog)
        {
            byName.TryAdd(p.ApiName, p);
        }

        return byName.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
