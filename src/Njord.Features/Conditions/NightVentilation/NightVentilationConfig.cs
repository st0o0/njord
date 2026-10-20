using Njord.Core.Configuration;

namespace Njord.Features.Conditions.NightVentilation;

public sealed class NightVentilationConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Lifestyle:NightVentilation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
