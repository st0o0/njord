using Njord.Core.Configuration;

namespace Njord.Features.Conditions.RagweedPollen;

public sealed class RagweedPollenConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:RagweedPollen";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
