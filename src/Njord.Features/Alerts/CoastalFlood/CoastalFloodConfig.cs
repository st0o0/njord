using Njord.Core.Configuration;

namespace Njord.Features.Alerts.CoastalFlood;

public sealed class CoastalFloodConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:CoastalFlood";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double SeaLevelThresholdM { get; set; } = 0.3;
    public double WaveHeightThresholdM { get; set; } = 1.5;
    public double WindSpeedThreshold { get; set; } = 4.2;
    public double RedSeaLevelM { get; set; } = 0.8;
    public int RedMinFactors { get; set; } = 1;
    public double OrangeSeaLevelM { get; set; } = 0.5;
    public int OrangeMinFactors { get; set; } = 0;
}
