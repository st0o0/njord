using Njord.Core.Configuration;

namespace Njord.Features.Alerts.HeavyRain;

public sealed class HeavyRainConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:HeavyRain";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double HourlyThreshold { get; set; } = 10.0;
    public double DailyThreshold { get; set; } = 25.0;
}
