using Njord.Core.Configuration;

namespace Njord.Features.Scores.Outdoor;

public sealed class OutdoorConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:Outdoor";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
