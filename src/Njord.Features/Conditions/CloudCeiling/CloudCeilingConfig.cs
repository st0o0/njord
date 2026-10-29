using Njord.Core.Configuration;

namespace Njord.Features.Conditions.CloudCeiling;

public sealed class CloudCeilingConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:CloudCeiling";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
