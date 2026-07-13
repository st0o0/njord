using System.Collections.Frozen;

namespace Njord.Domain;

public sealed record ModelSnapshot
{
    public static readonly ModelSnapshot Empty = new(
        FrozenDictionary<(string Location, WeatherModel Model), ModelForecast>.Empty, false);

    public IReadOnlyDictionary<(string Location, WeatherModel Model), ModelForecast> Entries { get; }
    public bool HasChanged { get; }

    private ModelSnapshot(
        IReadOnlyDictionary<(string Location, WeatherModel Model), ModelForecast> entries,
        bool hasChanged)
    {
        Entries = entries;
        HasChanged = hasChanged;
    }

    public ModelSnapshot Update(ModelForecast forecast)
    {
        var key = (forecast.Location, forecast.Model);

        if (Entries.TryGetValue(key, out var existing) && existing.Cycle == forecast.Cycle)
        {
            return new ModelSnapshot(Entries, false);
        }

        var dict = new Dictionary<(string, WeatherModel), ModelForecast>(Entries) { [key] = forecast };
        return new ModelSnapshot(dict, true);
    }

    public IReadOnlyList<WeatherModel> ModelsFor(string location)
    {
        var models = new List<WeatherModel>();
        foreach (var (key, _) in Entries)
        {
            if (key.Location == location)
                models.Add(key.Model);
        }
        return models;
    }
}
