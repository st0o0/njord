using Njord.Core.Configuration;

namespace Njord.Features.Conditions.InversionDetection;

public sealed class InversionDetectionConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Insight:InversionDetection";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
