using Njord.Core.Configuration;

namespace Njord.Features.Conditions.VentilationWindow;

public sealed class VentilationWindowConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Ventilation";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double MaxPm25 { get; set; } = 15.0;
    public double MaxPollen { get; set; } = 20.0;
    public double MinTemperatureCelsius { get; set; } = 15.0;
    public double MaxTemperatureCelsius { get; set; } = 28.0;
    public double MaxPrecipitationMmH { get; set; } = 0.1;
}
