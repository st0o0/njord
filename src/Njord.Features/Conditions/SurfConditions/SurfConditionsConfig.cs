using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SurfConditions;

public sealed class SurfConditionsConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Surf";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public int WindowHours { get; set; } = 24;
    public double MediumPeriodSeconds { get; set; } = 8.0;
    public double LongPeriodSeconds { get; set; } = 12.0;
    public double UsableHeightMinMeters { get; set; } = 0.5;
    public double IdealHeightMinMeters { get; set; } = 1.0;
    public double IdealHeightMaxMeters { get; set; } = 2.5;
    public double UsableHeightMaxMeters { get; set; } = 3.5;
    public double OffshoreToleranceDegrees { get; set; } = 45.0;
    public double ChoppyShare { get; set; } = 0.5;
    public double VeryChoppyShare { get; set; } = 0.75;
    public int PoorScoreCap { get; set; } = 39;
}
