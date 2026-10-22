using Njord.Core.Configuration;

namespace Njord.Features.Conditions.VentilationWindow;

public sealed class VentilationWindowConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Lifestyle:Ventilation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
