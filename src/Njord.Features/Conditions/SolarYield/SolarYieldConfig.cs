using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SolarYield;

public sealed class SolarYieldConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:SolarYield";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
