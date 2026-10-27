using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SnowDepthWatch;

public sealed class SnowDepthWatchConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:SnowDepthWatch";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double LightThreshold { get; set; } = 2;
    public double ModerateThreshold { get; set; } = 10;
    public double HeavyThreshold { get; set; } = 25;
    public double ExtremeThreshold { get; set; } = 50;
}
