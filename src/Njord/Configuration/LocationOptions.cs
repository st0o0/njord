namespace Njord.Configuration;

public sealed class LocationOptions
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public IList<string>? Models { get; set; }

    public IReadOnlyList<string> ResolveModels(IList<string> globalModels)
    {
        if (Models is null or { Count: 0 })
        {
            return [.. globalModels];
        }

        var merged = new List<string>(globalModels.Count + Models.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in globalModels)
        {
            if (seen.Add(m))
            {
                merged.Add(m);
            }
        }

        foreach (var m in Models)
        {
            if (seen.Add(m))
            {
                merged.Add(m);
            }
        }

        return merged;
    }
}
