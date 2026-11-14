using Njord.Core.Configuration;

namespace Njord.Features.Conditions.ApproachingFront;

public sealed class ApproachingFrontConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:FrontApproaching";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double PressureDropThresholdHpa { get; set; } = 3.0;
    public double WindShiftThresholdDegrees { get; set; } = 45.0;
    public int WindowHours { get; set; } = 6;
}
