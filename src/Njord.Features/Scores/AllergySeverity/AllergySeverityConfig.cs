using Njord.Core.Configuration;

namespace Njord.Features.Scores.AllergySeverity;

public sealed class AllergySeverityConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AllergySeverity";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int MinWindowScore { get; set; } = 50;
    public int MinWindowHours { get; set; } = 2;
    public int MaxWindows { get; set; } = 3;
    public double PollenReference { get; set; } = 100.0;
    public double GoodHourScore { get; set; } = 60.0;
    public double WindPollenModifier { get; set; } = 15.0;
    public double LowBoundaryLayerModifier { get; set; } = 15.0;
    public double DryAirModifier { get; set; } = 10.0;
    public double RainModifier { get; set; } = -30.0;
    public double HighHumidityModifier { get; set; } = -15.0;
    public double WindThreshold { get; set; } = 4.0;
    public double DryHumidityThreshold { get; set; } = 60.0;
    public double BoundaryLayerThreshold { get; set; } = 800.0;
    public double DryAirHumidityThreshold { get; set; } = 40.0;
    public double HighHumidityThreshold { get; set; } = 80.0;
}
