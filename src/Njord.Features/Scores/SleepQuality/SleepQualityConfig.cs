using Njord.Core.Configuration;

namespace Njord.Features.Scores.SleepQuality;

public sealed class SleepQualityConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SleepQuality";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public bool PollenSensitive { get; set; }
    public int NightStartHour { get; set; } = 22;
    public int NightEndHour { get; set; } = 6;
    public int MinWindowScore { get; set; } = 50;
    public int MinWindowHours { get; set; } = 2;
    public int MaxWindows { get; set; } = 3;
}
