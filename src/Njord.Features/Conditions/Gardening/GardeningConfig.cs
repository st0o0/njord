using Njord.Core.Configuration;

namespace Njord.Features.Conditions.Gardening;

public sealed class GardeningConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Gardening";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double FrostTemperatureCelsius { get; set; } = 2.0;
    public double RainThresholdMmH { get; set; } = 2.0;
    public double WindLimitMs { get; set; } = 10.0;
}
