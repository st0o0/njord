using Njord.Core.Configuration;

namespace Njord.Features.Alerts.Ice;

public sealed class IceConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Weather:Ice";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; } = true;
    public double Threshold { get; set; } = 2.0;
}
