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
    public double HighPollen { get; set; } = 50.0;
    public double Pm25Limit { get; set; } = 25.0;
    public double GustLimit { get; set; } = 11.1;
    public double CapeLimit { get; set; } = 1000.0;
    public int PreSleepHours { get; set; } = 6;
    public double HotThreshold { get; set; } = 25.0;
    public double WarmThreshold { get; set; } = 22.0;
    public double ColdThreshold { get; set; } = 5.0;
    public double HumidityThreshold { get; set; } = 80.0;
    public double HotPenalty { get; set; } = 30.0;
    public double WarmPenalty { get; set; } = 15.0;
    public double ColdPenalty { get; set; } = 10.0;
    public double HumidityPenalty { get; set; } = 10.0;
    public double Pm25Penalty { get; set; } = 15.0;
    public double PollenPenalty { get; set; } = 5.0;
    public double PollenSensitivePenalty { get; set; } = 20.0;
    public double GustPenalty { get; set; } = 10.0;
    public double ThunderstormPenalty { get; set; } = 15.0;
    public double VentilationHotPenalty { get; set; } = 30.0;
    public double VentilationHumidityPenalty { get; set; } = 20.0;
    public double VentilationGustPenalty { get; set; } = 15.0;
    public double VentilationRainPenalty { get; set; } = 30.0;
    public double VentilationPm25Penalty { get; set; } = 40.0;
    public double VentilationPollenPenalty { get; set; } = 15.0;
    public double VentilationPollenSensitivePenalty { get; set; } = 40.0;
}
