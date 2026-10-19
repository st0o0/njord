namespace Njord.Messages.Alerts;

public sealed record AlertSettings(
    SeverityThresholds Frost,
    SeverityThresholds Heat,
    SeverityThresholds StormGust,
    double HeavyRainHourlyThreshold,
    double HeavyRainDailyThreshold,
    double PressureDropThreshold,
    double PressureDropSevereThreshold,
    int FogPersistentHours,
    double CapeThreshold,
    double ThunderstormPrecipThreshold,
    double ThunderstormGustThreshold,
    double IceThreshold,
    SeverityThresholds WindChill,
    SeverityThresholds Visibility,
    SeverityThresholds TropicalNight,
    SeverityThresholds Humidity)
{
    public ConfidenceThresholds Confidence { get; init; } = ConfidenceThresholds.Default;
}
