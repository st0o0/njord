namespace Njord.Core.Configuration;

public static class WeatherVariables
{
    private const double VariablesPerCall = 10.0;

    private static readonly string[] Standard =
    [
        "temperature_2m", "apparent_temperature", "relative_humidity_2m", "dew_point_2m",
        "precipitation", "rain", "showers", "snowfall", "snow_depth", "weather_code",
        "cloud_cover", "cloud_cover_low", "cloud_cover_mid", "cloud_cover_high",
        "pressure_msl", "surface_pressure", "visibility", "is_day", "precipitation_probability",
        "wind_speed_10m", "wind_speed_80m", "wind_speed_120m", "wind_speed_180m",
        "wind_direction_10m", "wind_direction_80m", "wind_direction_120m", "wind_direction_180m",
        "wind_gusts_10m", "cape", "freezing_level_height", "vapour_pressure_deficit",
    ];

    private static readonly string[] Solar =
    [
        "shortwave_radiation", "direct_radiation", "diffuse_radiation",
        "direct_normal_irradiance", "sunshine_duration",
    ];

    public static IReadOnlyList<string> For(bool includeSolar) =>
        includeSolar ? [.. Standard, .. Solar] : Standard;

    public static int ApiCallWeight(int variableCount) =>
        (int)Math.Ceiling(variableCount / VariablesPerCall);
}
