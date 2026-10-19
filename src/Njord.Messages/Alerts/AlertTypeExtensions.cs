namespace Njord.Messages.Alerts;

public static class AlertTypeExtensions
{
    public static string ToTopicSegment(this AlertType type) => type switch
    {
        AlertType.Frost => "frost",
        AlertType.Heat => "heat",
        AlertType.Storm => "storm",
        AlertType.HeavyRain => "heavy-rain",
        AlertType.Uv => "uv",
        AlertType.Fog => "fog",
        AlertType.Snow => "snow",
        AlertType.PressureDrop => "pressure-drop",
        AlertType.Thunderstorm => "thunderstorm",
        AlertType.Ice => "ice",
        AlertType.WindChill => "wind-chill",
        AlertType.Visibility => "visibility",
        AlertType.TropicalNight => "tropical-night",
        AlertType.Humidity => "humidity",
        AlertType.Pollen => "pollen",
        AlertType.Pm25 => "pm2_5",
        AlertType.FloodRisk => "flood_risk",
        AlertType.FloodRain => "flood_rain",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static bool IsWeather(this AlertType type) => type switch
    {
        AlertType.Pollen or AlertType.Pm25 or AlertType.FloodRisk or AlertType.FloodRain => false,
        _ => true,
    };
}
