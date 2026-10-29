using Njord.Core.Configuration;

namespace Njord.Features.Alerts.FloodRain;

public sealed class FloodRainConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:FloodRain";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double HeavyRainThreshold { get; set; } = 30.0;
    public int ForecastDays { get; set; } = 7;
}
