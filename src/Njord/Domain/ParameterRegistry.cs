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
        return
        [
            // === Weather group — Hourly ===
            H("temperature_2m", "°C", "temperature", "temperature", ParameterGroup.Weather),
            H("apparent_temperature", "°C", "temperature", "apparent_temperature", ParameterGroup.Weather),
            H("relative_humidity_2m", "%", "humidity", "relative_humidity", ParameterGroup.Weather),
            H("dew_point_2m", "°C", "temperature", "dewpoint", ParameterGroup.Weather),
            H("precipitation", "mm", "precipitation", "precipitation", ParameterGroup.Weather),
            H("rain", "mm", "precipitation", "rain", ParameterGroup.Weather),
            H("showers", "mm", "precipitation", "showers", ParameterGroup.Weather),
            H("snowfall", "cm", null, "snowfall", ParameterGroup.Weather),
            H("snow_depth", "m", null, "snow_depth", ParameterGroup.Weather),
            H("weather_code", "wmo code", null, "weather_code", ParameterGroup.Weather),
            H("cloud_cover", "%", null, "cloud_cover", ParameterGroup.Weather),
            H("cloud_cover_low", "%", null, "cloud_cover_low", ParameterGroup.Weather),
            H("cloud_cover_mid", "%", null, "cloud_cover_mid", ParameterGroup.Weather),
            H("cloud_cover_high", "%", null, "cloud_cover_high", ParameterGroup.Weather),
            H("pressure_msl", "hPa", "atmospheric_pressure", "pressure_msl", ParameterGroup.Weather),
            H("surface_pressure", "hPa", "atmospheric_pressure", "surface_pressure", ParameterGroup.Weather),
            H("visibility", "m", "distance", "visibility", ParameterGroup.Weather),
            H("is_day", "", null, "is_day", ParameterGroup.Weather),
            H("precipitation_probability", "%", null, "precipitation_probability", ParameterGroup.Weather),
            H("wind_speed_10m", "m/s", "wind_speed", "wind_speed_10m", ParameterGroup.Weather),
            H("wind_speed_80m", "m/s", "wind_speed", "wind_speed_80m", ParameterGroup.Weather),
            H("wind_speed_120m", "m/s", "wind_speed", "wind_speed_120m", ParameterGroup.Weather),
            H("wind_speed_180m", "m/s", "wind_speed", "wind_speed_180m", ParameterGroup.Weather),
            H("wind_direction_10m", "°", null, "wind_direction_10m", ParameterGroup.Weather),
            H("wind_direction_80m", "°", null, "wind_direction_80m", ParameterGroup.Weather),
            H("wind_direction_120m", "°", null, "wind_direction_120m", ParameterGroup.Weather),
            H("wind_direction_180m", "°", null, "wind_direction_180m", ParameterGroup.Weather),
            H("wind_gusts_10m", "m/s", "wind_speed", "wind_gusts_10m", ParameterGroup.Weather),
            H("cape", "J/kg", null, "cape", ParameterGroup.Weather),
            H("freezing_level_height", "m", null, "freezing_level_height", ParameterGroup.Weather),
            H("vapour_pressure_deficit", "kPa", null, "vapour_pressure_deficit", ParameterGroup.Weather),

            // === Weather group — Daily ===
            D("temperature_2m_max", "°C", "temperature", "temperature_max", ParameterGroup.Weather),
            D("temperature_2m_min", "°C", "temperature", "temperature_min", ParameterGroup.Weather),
            D("apparent_temperature_max", "°C", "temperature", "apparent_temperature_max", ParameterGroup.Weather),
            D("apparent_temperature_min", "°C", "temperature", "apparent_temperature_min", ParameterGroup.Weather),
            D("weather_code", "wmo code", null, "weather_code_daily", ParameterGroup.Weather),
            D("precipitation_sum", "mm", "precipitation", "precipitation_sum", ParameterGroup.Weather),
            D("rain_sum", "mm", "precipitation", "rain_sum", ParameterGroup.Weather),
            D("showers_sum", "mm", "precipitation", "showers_sum", ParameterGroup.Weather),
            D("snowfall_sum", "cm", null, "snowfall_sum", ParameterGroup.Weather),
            D("precipitation_hours", "h", null, "precipitation_hours", ParameterGroup.Weather),
            D("precipitation_probability_max", "%", null, "precipitation_probability_max", ParameterGroup.Weather),
            D("wind_speed_10m_max", "m/s", "wind_speed", "wind_speed_max", ParameterGroup.Weather),
            D("wind_gusts_10m_max", "m/s", "wind_speed", "wind_gusts_max", ParameterGroup.Weather),
            D("wind_direction_10m_dominant", "°", null, "wind_direction_dominant", ParameterGroup.Weather),
            D("sunrise", "", "timestamp", "sunrise", ParameterGroup.Weather, ParameterValueType.TimeString),
            D("sunset", "", "timestamp", "sunset", ParameterGroup.Weather, ParameterValueType.TimeString),
            D("daylight_duration", "s", "duration", "daylight_duration", ParameterGroup.Weather),

            // === Solar group — Hourly ===
            H("shortwave_radiation", "W/m²", "irradiance", "shortwave_radiation", ParameterGroup.Solar),
            H("direct_radiation", "W/m²", "irradiance", "direct_radiation", ParameterGroup.Solar),
            H("diffuse_radiation", "W/m²", "irradiance", "diffuse_radiation", ParameterGroup.Solar),
            H("direct_normal_irradiance", "W/m²", "irradiance", "direct_normal_irradiance", ParameterGroup.Solar),
            H("global_tilted_irradiance", "W/m²", "irradiance", "global_tilted_irradiance", ParameterGroup.Solar),
            H("terrestrial_radiation", "W/m²", "irradiance", "terrestrial_radiation", ParameterGroup.Solar),
            H("sunshine_duration", "s", null, "sunshine_duration", ParameterGroup.Solar),
            H("uv_index", "", null, "uv_index", ParameterGroup.Solar),
            H("uv_index_clear_sky", "", null, "uv_index_clear_sky", ParameterGroup.Solar),

            // === Solar group — Daily ===
            D("shortwave_radiation_sum", "MJ/m²", null, "shortwave_radiation_sum", ParameterGroup.Solar),
            D("sunshine_duration", "s", null, "sunshine_duration_daily", ParameterGroup.Solar),
            D("uv_index_max", "", null, "uv_index_max", ParameterGroup.Solar),
            D("uv_index_clear_sky_max", "", null, "uv_index_clear_sky_max", ParameterGroup.Solar),

            // === Soil group — Hourly ===
            H("soil_temperature_0cm", "°C", "temperature", "soil_temperature_0cm", ParameterGroup.Soil),
            H("soil_temperature_6cm", "°C", "temperature", "soil_temperature_6cm", ParameterGroup.Soil),
            H("soil_temperature_18cm", "°C", "temperature", "soil_temperature_18cm", ParameterGroup.Soil),
            H("soil_temperature_54cm", "°C", "temperature", "soil_temperature_54cm", ParameterGroup.Soil),
            H("soil_moisture_0_to_1cm", "m³/m³", null, "soil_moisture_0_to_1cm", ParameterGroup.Soil),
            H("soil_moisture_1_to_3cm", "m³/m³", null, "soil_moisture_1_to_3cm", ParameterGroup.Soil),
            H("soil_moisture_3_to_9cm", "m³/m³", null, "soil_moisture_3_to_9cm", ParameterGroup.Soil),
            H("soil_moisture_9_to_27cm", "m³/m³", null, "soil_moisture_9_to_27cm", ParameterGroup.Soil),
            H("soil_moisture_27_to_81cm", "m³/m³", null, "soil_moisture_27_to_81cm", ParameterGroup.Soil),
            H("evapotranspiration", "mm", null, "evapotranspiration", ParameterGroup.Soil),
            H("et0_fao_evapotranspiration", "mm", null, "et0_fao_evapotranspiration", ParameterGroup.Soil),

            // === Soil group — Daily ===
            D("et0_fao_evapotranspiration", "mm", null, "et0_fao_evapotranspiration_daily", ParameterGroup.Soil),
        ];

        // Shorthand helpers
        static ParameterDef H(string api, string unit, string? dc, string json, ParameterGroup g, ParameterValueType vt = ParameterValueType.Numeric)
            => new(api, unit, dc, json, g, ParameterGranularity.Hourly, vt);

        static ParameterDef D(string api, string unit, string? dc, string json, ParameterGroup g, ParameterValueType vt = ParameterValueType.Numeric)
            => new(api, unit, dc, json, g, ParameterGranularity.Daily, vt);
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
