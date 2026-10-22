namespace Njord.Core.Configuration;

public sealed class NjordOptions : IConfigSection
{
    private const string SectionName = "Njord";
    static string IConfigSection.SectionName => SectionName;

    public IList<LocationOptions> Locations { get; set; } = [];

    public LocationOptions? FindLocation(string name) =>
        Locations.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
