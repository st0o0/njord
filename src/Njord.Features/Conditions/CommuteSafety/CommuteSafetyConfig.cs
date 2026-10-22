using Njord.Core.Configuration;

namespace Njord.Features.Conditions.CommuteSafety;

public sealed class CommuteSafetyConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:Commute";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
}
