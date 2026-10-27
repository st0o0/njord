using Njord.Core.Configuration;

namespace Njord.Features.Conditions.ElevatedWind;

public sealed class ElevatedWindConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Energy:ElevatedWind";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
