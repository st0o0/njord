namespace Njord.Compute.Analysis;

public enum WeatherCategory
{
    Clear,
    Fog,
    Drizzle,
    Rain,
    Snow,
    Showers,
    Thunderstorm,
    Unknown,
}

public static class WeatherCategoryExtensions
{
    public static WeatherCategory FromWmoCode(int code) => code switch
    {
        >= 0 and <= 3 => WeatherCategory.Clear,
        >= 45 and <= 48 => WeatherCategory.Fog,
        >= 51 and <= 57 => WeatherCategory.Drizzle,
        >= 61 and <= 67 => WeatherCategory.Rain,
        >= 71 and <= 77 => WeatherCategory.Snow,
        >= 80 and <= 86 => WeatherCategory.Showers,
        >= 95 and <= 99 => WeatherCategory.Thunderstorm,
        _ => WeatherCategory.Unknown,
    };

    public static string ToWireName(this WeatherCategory category) => category switch
    {
        WeatherCategory.Clear => "clear",
        WeatherCategory.Fog => "fog",
        WeatherCategory.Drizzle => "drizzle",
        WeatherCategory.Rain => "rain",
        WeatherCategory.Snow => "snow",
        WeatherCategory.Showers => "showers",
        WeatherCategory.Thunderstorm => "thunderstorm",
        WeatherCategory.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    public static string ToDescription(this WeatherChangeResult change) =>
        $"{change.From.ToWireName()} → {change.To.ToWireName()}";
}
