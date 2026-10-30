namespace Njord.Core.Configuration;

public sealed class NjordOptions : IConfigSection
{
    private const string SectionName = "Njord";
    static string IConfigSection.SectionName => SectionName;

    public IList<LocationOptions> Locations { get; set; } = [];

    public IReadOnlyList<Location> ResolvedLocations { get; set; } = [];

    public Location? FindLocation(string name)
    {
        foreach (var location in ResolvedLocations)
        {
            if (location.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return location;
        }

        return null;
    }
}
