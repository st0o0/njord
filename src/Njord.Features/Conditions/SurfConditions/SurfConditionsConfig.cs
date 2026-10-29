using Njord.Core.Configuration;

namespace Njord.Features.Conditions.SurfConditions;

public sealed class SurfConditionsConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Surf";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
