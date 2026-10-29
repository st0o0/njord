using Njord.Core.Configuration;

namespace Njord.Features.Conditions.NightVentilation;

public sealed class NightVentilationConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:NightVentilation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
