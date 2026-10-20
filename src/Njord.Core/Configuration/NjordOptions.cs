namespace Njord.Core.Configuration;

public sealed class NjordOptions : IConfigSection
{
    public const string SectionName = "Njord";
    static string IConfigSection.SectionName => SectionName;

    public IList<LocationOptions> Locations { get; set; } = [];
}
