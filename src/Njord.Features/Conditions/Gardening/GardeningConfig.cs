using Njord.Core.Configuration;

namespace Njord.Features.Conditions.Gardening;

public sealed class GardeningConfig : IConfigSection
{
    public const string SectionName = "Njord:Features:Lifestyle:Gardening";
    static string IConfigSection.SectionName => SectionName;
    public bool Enabled { get; set; }
}
