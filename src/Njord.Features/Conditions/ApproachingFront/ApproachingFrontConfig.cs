using Njord.Core.Configuration;

namespace Njord.Features.Conditions.ApproachingFront;

public sealed class ApproachingFrontConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Insight:FrontApproaching";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
