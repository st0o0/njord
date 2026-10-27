using Njord.Core.Configuration;

namespace Njord.Features.Conditions.OceanCurrents;

public sealed class OceanCurrentsConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Marine:OceanCurrents";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
    public double StrongCurrentThresholdKmh { get; set; } = 3.0;
}
