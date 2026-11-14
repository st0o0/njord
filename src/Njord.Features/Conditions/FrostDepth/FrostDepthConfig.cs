using Njord.Core.Configuration;

namespace Njord.Features.Conditions.FrostDepth;

public sealed class FrostDepthConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:FrostDepth";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PipeDepthCm { get; set; } = 60.0;
    public double StefanAlpha { get; set; } = 2.4;
    public double PipeApproachFraction { get; set; } = 0.8;
    public double SurfaceThresholdCm { get; set; } = 5.0;
    public double ShallowThresholdCm { get; set; } = 15.0;
    public double ModerateThresholdCm { get; set; } = 30.0;
}
