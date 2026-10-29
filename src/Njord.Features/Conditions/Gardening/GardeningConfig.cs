using Njord.Core.Configuration;

namespace Njord.Features.Conditions.Gardening;

public sealed class GardeningConfig : IConfigSection
{
    private const string SectionName = "Njord:Features:Gardening";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
