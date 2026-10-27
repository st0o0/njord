using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Ice;

public sealed class IceConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Forecast:Ice";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double Threshold { get; set; } = 2.0;
}
