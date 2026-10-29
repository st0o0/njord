using Njord.Core.Configuration;

namespace Njord.Features.Conditions.AtmosphericStability;

public sealed class AtmosphericStabilityConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:AtmosphericStability";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
