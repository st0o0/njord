using Njord.Core.Configuration;

namespace Njord.Features.Alerts.PressureDrop;

public sealed class PressureDropConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Weather:PressureDrop";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double Threshold { get; set; } = 5.0;
    public double SevereThreshold { get; set; } = 10.0;
}
