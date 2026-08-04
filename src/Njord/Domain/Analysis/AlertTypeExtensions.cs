namespace Njord.Domain.Analysis;

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
        _ => type.ToString().ToLowerInvariant(),
    };
}
