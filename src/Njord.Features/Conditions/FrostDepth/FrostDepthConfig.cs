using Njord.Core.Configuration;

namespace Njord.Features.Conditions.FrostDepth;

public sealed class FrostDepthConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:FrostDepth";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PipeDepthCm { get; set; } = 60.0;
}
