namespace Njord.Domain;

public static class ParameterRegistry
{
    private static readonly Dictionary<string, ParameterDef> ByName;

    private static readonly List<ParameterDef> AllList;

    static ParameterRegistry()
    {
        AllList = BuildAll();
        ByName = new Dictionary<string, ParameterDef>(StringComparer.Ordinal);
        foreach (var p in AllList)
        {
            ByName.TryAdd(p.ApiName, p);
        }
    }

    public static IReadOnlyCollection<ParameterDef> All => AllList;

    public static IReadOnlyList<ParameterDef> GetByGroup(ParameterGroup group)
        => AllList.Where(p => p.Group == group).ToList();

    public static ParameterDef? GetByApiName(string name)
        => ByName.GetValueOrDefault(name);

    public static ParameterDef? GetByApiName(string name, ParameterGranularity granularity)
        => AllList.FirstOrDefault(p => p.ApiName == name && p.Granularity == granularity);

    public static ResolvedParameterSet Resolve(
        IEnumerable<string> groups,
        IEnumerable<string> extra,
        IEnumerable<string> exclude)
    {
        var errors = new List<string>();

        var parsedGroups = new List<ParameterGroup>();
        foreach (var g in groups)
        {
            if (Enum.TryParse<ParameterGroup>(g, ignoreCase: true, out var pg))
                parsedGroups.Add(pg);
            else
                errors.Add($"Unknown parameter group: '{g}'. Valid groups: {string.Join(", ", Enum.GetNames<ParameterGroup>())}");
        }

        var extraParams = new List<ParameterDef>();
        foreach (var name in extra)
        {
            if (GetByApiName(name) is { } p)
                extraParams.Add(p);
            else
                errors.Add($"Unknown parameter in Extra: '{name}'");
        }

        var excludeSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in exclude)
        {
            if (GetByApiName(name) is not null)
                excludeSet.Add(name);
            else
                errors.Add($"Unknown parameter in Exclude: '{name}'");
        }

        if (errors.Count > 0)
            throw new ParameterResolutionException(errors);

        var resolved = parsedGroups
            .SelectMany(g => AllList.Where(p => p.Group == g))
            .Concat(extraParams)
            .Where(p => !excludeSet.Contains(p.ApiName))
            .Distinct()
            .ToList();

        if (resolved.Count == 0)
            throw new ParameterResolutionException(["The resolved parameter set is empty. Enable at least one group or add parameters via Extra."]);

        return new ResolvedParameterSet(
            resolved.Where(p => p.Granularity == ParameterGranularity.Hourly).ToList(),
            resolved.Where(p => p.Granularity == ParameterGranularity.Daily).ToList());
    }

    private static List<ParameterDef> BuildAll()
    {
        // Shorthand helpers
        static ParameterDef H(string api, string unit, string? dc, string json, string name, ParameterGroup g, ParameterValueType vt = ParameterValueType.Numeric)
            => new(api, unit, dc, json, name, g, ParameterGranularity.Hourly, vt);
        static ParameterDef D(string api, string unit, string? dc, string json, string name, ParameterGroup g, ParameterValueType vt = ParameterValueType.Numeric)
            => new(api, unit, dc, json, name, g, ParameterGranularity.Daily, vt);

        return
        [
            // === Weather group — Hourly ===
            H("temperature_2m", "°C", "temperature", "temperature", "Temperature (2m)", ParameterGroup.Weather),
            H("apparent_temperature", "°C", "temperature", "apparent_temperature", "Apparent Temperature", ParameterGroup.Weather),
            H("relative_humidity_2m", "%", "humidity", "relative_humidity", "Relative Humidity (2m)", ParameterGroup.Weather),
            H("dew_point_2m", "°C", "temperature", "dewpoint", "Dew Point (2m)", ParameterGroup.Weather),
            H("precipitation", "mm", "precipitation", "precipitation", "Precipitation", ParameterGroup.Weather),
            H("rain", "mm", "precipitation", "rain", "Rain", ParameterGroup.Weather),
            H("showers", "mm", "precipitation", "showers", "Showers", ParameterGroup.Weather),
            H("snowfall", "cm", null, "snowfall", "Snowfall", ParameterGroup.Weather),
            H("snow_depth", "m", null, "snow_depth", "Snow Depth", ParameterGroup.Weather),
            H("weather_code", "wmo code", null, "weather_code", "Weather Code (WMO)", ParameterGroup.Weather),
            H("cloud_cover", "%", null, "cloud_cover", "Cloud Cover", ParameterGroup.Weather),
            H("cloud_cover_low", "%", null, "cloud_cover_low", "Cloud Cover (Low)", ParameterGroup.Weather),
            H("cloud_cover_mid", "%", null, "cloud_cover_mid", "Cloud Cover (Mid)", ParameterGroup.Weather),
            H("cloud_cover_high", "%", null, "cloud_cover_high", "Cloud Cover (High)", ParameterGroup.Weather),
            H("pressure_msl", "hPa", "atmospheric_pressure", "pressure_msl", "Pressure (MSL)", ParameterGroup.Weather),
            H("surface_pressure", "hPa", "atmospheric_pressure", "surface_pressure", "Surface Pressure", ParameterGroup.Weather),
            H("visibility", "m", "distance", "visibility", "Visibility", ParameterGroup.Weather),
            H("is_day", "", null, "is_day", "Is Day", ParameterGroup.Weather),
            H("precipitation_probability", "%", null, "precipitation_probability", "Precipitation Probability", ParameterGroup.Weather),
            H("wind_speed_10m", "m/s", "wind_speed", "wind_speed_10m", "Wind Speed (10m)", ParameterGroup.Weather),
            H("wind_speed_80m", "m/s", "wind_speed", "wind_speed_80m", "Wind Speed (80m)", ParameterGroup.Weather),
            H("wind_speed_120m", "m/s", "wind_speed", "wind_speed_120m", "Wind Speed (120m)", ParameterGroup.Weather),
            H("wind_speed_180m", "m/s", "wind_speed", "wind_speed_180m", "Wind Speed (180m)", ParameterGroup.Weather),
            H("wind_direction_10m", "°", null, "wind_direction_10m", "Wind Direction (10m)", ParameterGroup.Weather),
            H("wind_direction_80m", "°", null, "wind_direction_80m", "Wind Direction (80m)", ParameterGroup.Weather),
            H("wind_direction_120m", "°", null, "wind_direction_120m", "Wind Direction (120m)", ParameterGroup.Weather),
            H("wind_direction_180m", "°", null, "wind_direction_180m", "Wind Direction (180m)", ParameterGroup.Weather),
            H("wind_gusts_10m", "m/s", "wind_speed", "wind_gusts_10m", "Wind Gusts (10m)", ParameterGroup.Weather),
            H("cape", "J/kg", null, "cape", "CAPE", ParameterGroup.Weather),
            H("freezing_level_height", "m", null, "freezing_level_height", "Freezing Level Height", ParameterGroup.Weather),
            H("vapour_pressure_deficit", "kPa", null, "vapour_pressure_deficit", "Vapour Pressure Deficit", ParameterGroup.Weather),

            // === Weather group — Daily ===
            D("temperature_2m_max", "°C", "temperature", "temperature_max", "Temperature Max", ParameterGroup.Weather),
            D("temperature_2m_min", "°C", "temperature", "temperature_min", "Temperature Min", ParameterGroup.Weather),
            D("apparent_temperature_max", "°C", "temperature", "apparent_temperature_max", "Apparent Temperature Max", ParameterGroup.Weather),
            D("apparent_temperature_min", "°C", "temperature", "apparent_temperature_min", "Apparent Temperature Min", ParameterGroup.Weather),
            D("weather_code", "wmo code", null, "weather_code_daily", "Weather Code (Daily)", ParameterGroup.Weather),
            D("precipitation_sum", "mm", "precipitation", "precipitation_sum", "Precipitation Sum", ParameterGroup.Weather),
            D("rain_sum", "mm", "precipitation", "rain_sum", "Rain Sum", ParameterGroup.Weather),
            D("showers_sum", "mm", "precipitation", "showers_sum", "Showers Sum", ParameterGroup.Weather),
            D("snowfall_sum", "cm", null, "snowfall_sum", "Snowfall Sum", ParameterGroup.Weather),
            D("precipitation_hours", "h", null, "precipitation_hours", "Precipitation Hours", ParameterGroup.Weather),
            D("precipitation_probability_max", "%", null, "precipitation_probability_max", "Precipitation Probability Max", ParameterGroup.Weather),
            D("wind_speed_10m_max", "m/s", "wind_speed", "wind_speed_max", "Wind Speed Max", ParameterGroup.Weather),
            D("wind_gusts_10m_max", "m/s", "wind_speed", "wind_gusts_max", "Wind Gusts Max", ParameterGroup.Weather),
            D("wind_direction_10m_dominant", "°", null, "wind_direction_dominant", "Wind Direction Dominant", ParameterGroup.Weather),
            D("sunrise", "", "timestamp", "sunrise", "Sunrise", ParameterGroup.Weather, ParameterValueType.TimeString),
            D("sunset", "", "timestamp", "sunset", "Sunset", ParameterGroup.Weather, ParameterValueType.TimeString),
            D("daylight_duration", "s", "duration", "daylight_duration", "Daylight Duration", ParameterGroup.Weather),

            // === Solar group — Hourly ===
            H("shortwave_radiation", "W/m²", "irradiance", "shortwave_radiation", "Shortwave Radiation", ParameterGroup.Solar),
            H("direct_radiation", "W/m²", "irradiance", "direct_radiation", "Direct Radiation", ParameterGroup.Solar),
            H("diffuse_radiation", "W/m²", "irradiance", "diffuse_radiation", "Diffuse Radiation", ParameterGroup.Solar),
            H("direct_normal_irradiance", "W/m²", "irradiance", "direct_normal_irradiance", "Direct Normal Irradiance", ParameterGroup.Solar),
            H("global_tilted_irradiance", "W/m²", "irradiance", "global_tilted_irradiance", "Global Tilted Irradiance", ParameterGroup.Solar),
            H("terrestrial_radiation", "W/m²", "irradiance", "terrestrial_radiation", "Terrestrial Radiation", ParameterGroup.Solar),
            H("sunshine_duration", "s", null, "sunshine_duration", "Sunshine Duration", ParameterGroup.Solar),
            H("uv_index", "", null, "uv_index", "UV Index", ParameterGroup.Solar),
            H("uv_index_clear_sky", "", null, "uv_index_clear_sky", "UV Index (Clear Sky)", ParameterGroup.Solar),

            // === Solar group — Daily ===
            D("shortwave_radiation_sum", "MJ/m²", null, "shortwave_radiation_sum", "Shortwave Radiation Sum", ParameterGroup.Solar),
            D("sunshine_duration", "s", null, "sunshine_duration_daily", "Sunshine Duration (Daily)", ParameterGroup.Solar),
            D("uv_index_max", "", null, "uv_index_max", "UV Index Max", ParameterGroup.Solar),
            D("uv_index_clear_sky_max", "", null, "uv_index_clear_sky_max", "UV Index Clear Sky Max", ParameterGroup.Solar),

            // === Soil group — Hourly ===
            H("soil_temperature_0cm", "°C", "temperature", "soil_temperature_0cm", "Soil Temperature (0cm)", ParameterGroup.Soil),
            H("soil_temperature_6cm", "°C", "temperature", "soil_temperature_6cm", "Soil Temperature (6cm)", ParameterGroup.Soil),
            H("soil_temperature_18cm", "°C", "temperature", "soil_temperature_18cm", "Soil Temperature (18cm)", ParameterGroup.Soil),
            H("soil_temperature_54cm", "°C", "temperature", "soil_temperature_54cm", "Soil Temperature (54cm)", ParameterGroup.Soil),
            H("soil_moisture_0_to_1cm", "m³/m³", null, "soil_moisture_0_to_1cm", "Soil Moisture (0-1cm)", ParameterGroup.Soil),
            H("soil_moisture_1_to_3cm", "m³/m³", null, "soil_moisture_1_to_3cm", "Soil Moisture (1-3cm)", ParameterGroup.Soil),
            H("soil_moisture_3_to_9cm", "m³/m³", null, "soil_moisture_3_to_9cm", "Soil Moisture (3-9cm)", ParameterGroup.Soil),
            H("soil_moisture_9_to_27cm", "m³/m³", null, "soil_moisture_9_to_27cm", "Soil Moisture (9-27cm)", ParameterGroup.Soil),
            H("soil_moisture_27_to_81cm", "m³/m³", null, "soil_moisture_27_to_81cm", "Soil Moisture (27-81cm)", ParameterGroup.Soil),
            H("evapotranspiration", "mm", null, "evapotranspiration", "Evapotranspiration", ParameterGroup.Soil),
            H("et0_fao_evapotranspiration", "mm", null, "et0_fao_evapotranspiration", "ET₀ FAO Evapotranspiration", ParameterGroup.Soil),

            // === Soil group — Daily ===
            D("et0_fao_evapotranspiration", "mm", null, "et0_fao_evapotranspiration_daily", "ET₀ FAO Evapotranspiration (Daily)", ParameterGroup.Soil),
        ];
    }
}

public sealed class ResolvedParameterSet(
    IReadOnlyList<ParameterDef> hourly,
    IReadOnlyList<ParameterDef> daily)
{
    public IReadOnlyList<ParameterDef> Hourly { get; } = hourly;
    public IReadOnlyList<ParameterDef> Daily { get; } = daily;

    public int HourlyCount => Hourly.Count;
    public int ApiCallWeight => (int)Math.Ceiling(HourlyCount / 10.0);
}

public sealed class ParameterResolutionException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
