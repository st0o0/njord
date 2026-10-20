namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainConfig
{
    public const string SectionName = "Njord:Features:Weather:HeavyRain";
    public bool Enabled { get; set; } = true;
    public double HourlyThreshold { get; set; } = 10.0;
    public double DailyThreshold { get; set; } = 25.0;
}
