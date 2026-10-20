using Njord.Core.Configuration;

namespace Njord.Features.Conditions.VentilationWindow;

public sealed class VentilationWindowConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Lifestyle:Ventilation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
